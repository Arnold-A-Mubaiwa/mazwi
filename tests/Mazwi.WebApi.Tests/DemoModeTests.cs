using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Mazwi.Domain;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Mazwi.WebApi.Tests;

public sealed class DemoModeTests
{
    private static string DemoFolder()
    {
        string? folder = AppContext.BaseDirectory;
        while (folder is not null && !File.Exists(Path.Combine(folder, "Mazwi.sln")))
        {
            folder = Path.GetDirectoryName(folder);
        }

        return Path.Combine(folder ?? throw new InvalidOperationException("Repository root not found."), "demo");
    }

    [Theory]
    [InlineData("Host=127.0.0.1;Port=5439;Database=mazwi_demo;Username=postgres", false, true)]
    [InlineData("Host=127.0.0.1;Port=5439;Database=mazwi_demo;Username=postgres", true, false)]
    [InlineData("Host=localhost;Database=mazwi;Username=postgres", false, false)]
    [InlineData("Host=localhost;Database=mazwi_tests;Username=postgres", false, false)]
    [InlineData("Host=localhost;Database=mazwi_demo_old;Username=postgres", false, false)]
    [InlineData("", false, false)]
    [InlineData(null, false, false)]
    public void Allows_OnlyDemoDatabasesWhenNotHosted(string? connectionString, bool hosted, bool expected) =>
        Assert.Equal(expected, DemoMode.Allows(connectionString, hosted));

    [Fact]
    public void Load_RepositoryDemoFolder_HasValidArtistsStoriesAndRecordings()
    {
        // Arrange
        string folder = DemoFolder();
        // Act
        DemoContent content = DemoMode.Load(folder);
        // Assert
        Assert.Equal(2, content.Artists.Count);
        Assert.Equal(3, content.Stories.Count);
        Assert.True(content.Password.Length >= 10);
        Assert.All(content.Artists, artist => _ = new EmailAddress(artist.Email));
        Assert.All(content.Stories, story =>
        {
            _ = new StoryDetails(story.Title, story.ArtistName, story.Community, story.Country, story.Type, story.Language);
            Assert.InRange(story.Artist, 0, content.Artists.Count - 1);
            using Stream recording = DemoMode.Open(folder, story);
            Assert.InRange(recording.Length, 10_000, 1_000_000);
            Assert.False(string.IsNullOrWhiteSpace(story.Words));
        });
        Assert.Equal(["xh", "st", "tn"], content.Stories.Select(story => story.Language));
        Assert.True(File.Exists(Path.Combine(folder, "ATTRIBUTION.md")));
    }

    [Theory]
    [InlineData("../README.md")]
    [InlineData("..\\Mazwi.sln")]
    [InlineData("C:/Windows/win.ini")]
    public void Open_PathOutsideDemoFolder_IsRefused(string recording)
    {
        // Arrange
        var story = new DemoStory(0, "T", "A", "C", "ZA", "story", "xh", recording, "Words");
        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => DemoMode.Open(DemoFolder(), story));
    }

    [Fact]
    public async Task SeedAsync_RealPostgres_PublishesStoriesThatViewersAndArtistsCanUse()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string root = Path.Combine(Path.GetTempPath(), "mazwi-demo-tests", Guid.NewGuid().ToString("N"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mazwi"] = connectionString, ["MediaStorage:Root"] = root, ["Mazwi:AccountRequestsPerMinute"] = "100" }));
        });
        string folder = DemoFolder();
        DemoContent repositoryContent = DemoMode.Load(folder);
        string run = Guid.NewGuid().ToString("N")[..8];
        // Unique emails and titles, so the shared test database cannot collide with earlier runs.
        DemoContent content = repositoryContent with
        {
            Artists = repositoryContent.Artists.Select(artist => artist with { Email = $"{run}-{artist.Email}" }).ToArray(),
            Stories = repositoryContent.Stories.Select(story => story with { Title = $"{story.Title} {run}" }).ToArray(),
        };
        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
        try
        {
            // Act
            DemoSeedResult first, second;
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                IDemoSeedService seeder = scope.ServiceProvider.GetRequiredService<IDemoSeedService>();
                first = await seeder.SeedAsync(content, story => DemoMode.Open(folder, story), CancellationToken.None);
                second = await seeder.SeedAsync(content, story => DemoMode.Open(folder, story), CancellationToken.None);
            }

            // Assert
            Assert.Equal(new DemoSeedResult(false, 2, 3), first);
            Assert.Equal(new DemoSeedResult(true, 0, 0), second);
            using HttpClient artist = factory.CreateClient();
            HttpResponseMessage signIn = await artist.PostAsJsonAsync("/api/account/signin", new { content.Artists[0].Email, content.Password });
            Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);
            Assert.True((await signIn.Content.ReadFromJsonAsync<ArtistResponse>())!.EmailVerified);
            StudioPageResponse studio = (await artist.GetFromJsonAsync<StudioPageResponse>("/api/studio?limit=50"))!;
            StoryResponse[] mine = studio.Items.Where(item => item.Title.EndsWith(run, StringComparison.Ordinal)).ToArray();
            Assert.Equal(2, mine.Length);
            Assert.All(mine, item => Assert.Equal("published", item.Status));
            using HttpClient viewer = factory.CreateClient();
            viewer.DefaultRequestHeaders.Add("X-Test-Artist", "none");
            StoryViewResponse xhosa = (await viewer.GetFromJsonAsync<StoryViewResponse>($"/api/feed/{mine.Single(item => item.Language == "xh").Id}?language=en"))!;
            Assert.Equal("xh", xhosa.TextLanguage);
            Assert.Equal(content.Stories[0].Words, xhosa.Text);
            Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(xhosa.MediaUrl)).StatusCode);
        }
        finally
        {
            var artists = new ArtistRepository(dataSource);
            var stories = new StoryRepository(dataSource);
            foreach (DemoArtist demo in content.Artists)
            {
                if (await artists.FindByEmailAsync(new EmailAddress(demo.Email), CancellationToken.None) is Artist found)
                {
                    foreach (Guid id in await stories.ListOwnedIdsAsync(found.Id, 0, 50, CancellationToken.None))
                    {
                        await stories.DeleteAsync(id, CancellationToken.None);
                    }

                    await artists.DeleteAsync(found.Id, CancellationToken.None);
                }
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
