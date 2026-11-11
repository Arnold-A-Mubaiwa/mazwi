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
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.WebApi.Tests;

public sealed class StoryRemovalEndpointTests
{
    private static readonly Guid StoryId = Guid.NewGuid();
    private static readonly Guid Version = Guid.NewGuid();
    private readonly IStoryRemovalService _service = Substitute.For<IStoryRemovalService>();

    private WebApplicationFactory<Program> Factory(StoryAccess access = StoryAccess.Allowed) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.SignInTestArtist(access);
        builder.ConfigureServices(services => { services.RemoveAll<IStoryRemovalService>(); services.AddSingleton(_service); });
    });

    [Fact]
    public async Task Unpublish_Owner_ReturnsUnpublishedDraft()
    {
        // Arrange
        _service.UnpublishAsync(StoryId, Version, Arg.Any<CancellationToken>()).Returns(new StoryDraftDto(StoryId, "Story", "Artist", "sn", "unpublished", "Words", [], Guid.NewGuid()));
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/api/stories/{StoryId}/unpublish", new { reviewVersion = Version });
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("unpublished", (await response.Content.ReadFromJsonAsync<StoryDraftResponse>())!.Status);
    }

    [Fact]
    public async Task Unpublish_NotPublishedOrStale_Returns400Or409()
    {
        // Arrange
        _service.UnpublishAsync(StoryId, Version, Arg.Any<CancellationToken>()).ThrowsAsync(new DomainException("This story is not published."));
        _service.UnpublishAsync(StoryId, Guid.Empty, Arg.Any<CancellationToken>()).ThrowsAsync(new StoryConflictException());
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/stories/{StoryId}/unpublish", new { reviewVersion = Version })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/stories/{StoryId}/unpublish", new { reviewVersion = Guid.Empty })).StatusCode);
    }

    [Fact]
    public async Task Delete_Owner_Returns204AndMissingReturns404()
    {
        // Arrange
        _service.DeleteAsync(StoryId, Version, Arg.Any<CancellationToken>()).Returns(true);
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/stories/{StoryId}?version={Version}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/api/stories/{StoryId}?version={Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Delete_OtherArtistOrSignedOut_Returns403Or401WithoutDeleting()
    {
        // Arrange
        await using WebApplicationFactory<Program> denied = Factory(StoryAccess.Denied);
        using HttpClient client = denied.CreateClient();
        using var signedOut = new HttpRequestMessage(HttpMethod.Delete, $"/api/stories/{StoryId}?version={Version}");
        signedOut.Headers.Add(TestArtist.Header, TestArtist.Anonymous);
        // Act / Assert
        Assert.Equal(HttpStatusCode.Forbidden, (await client.DeleteAsync($"/api/stories/{StoryId}?version={Version}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(signedOut)).StatusCode);
        await _service.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default);
    }

    [Fact]
    public async Task DeleteAccount_LocalPostgres_RemovesStoriesFilesAndAccountThenEndsSession()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string root = Path.Combine(Path.GetTempPath(), "mazwi-removal-tests", Guid.NewGuid().ToString("N"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mazwi"] = connectionString, ["MediaStorage:Root"] = root, ["Mazwi:AccountRequestsPerMinute"] = "100" }));
        });
        using HttpClient client = factory.CreateClient();
        Guid? artistId = null;
        var storyIds = new List<Guid>();
        try
        {
            string email = $"removal-{Guid.NewGuid():N}@mazwi.test";
            HttpResponseMessage signUp = await client.PostAsJsonAsync("/api/account/signup", new { Email = email, Password = "a long test password" });
            artistId = (await signUp.Content.ReadFromJsonAsync<ArtistResponse>())!.Id;
            for (int index = 0; index < 2; index++)
            {
                using MultipartFormDataContent form = StoriesEndpointTests.Form();
                storyIds.Add((await (await client.PostAsync("/api/stories", form)).Content.ReadFromJsonAsync<StoryResponse>())!.Id);
            }

            StoryDraftResponse first = (await client.GetFromJsonAsync<StoryDraftResponse>($"/api/stories/{storyIds[0]}/drafts"))!;
            // Act
            HttpResponseMessage stale = await client.DeleteAsync($"/api/stories/{storyIds[0]}?version={Guid.NewGuid()}");
            HttpResponseMessage deleted = await client.DeleteAsync($"/api/stories/{storyIds[0]}?version={first.ReviewVersion}");
            HttpResponseMessage wrongPassword = await client.PostAsJsonAsync("/api/account/delete", new { password = "not my password" });
            HttpResponseMessage account = await client.PostAsJsonAsync("/api/account/delete", new { password = "a long test password" });
            // Assert
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, wrongPassword.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, account.StatusCode);
            Assert.Empty(Directory.Exists(root) ? Directory.GetFiles(root) : []);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
            Assert.Null(await new ArtistRepository(dataSource).GetAsync(artistId.Value, CancellationToken.None));
            foreach (Guid id in storyIds)
            {
                Assert.Null(await new StoryRepository(dataSource).GetAsync(id, CancellationToken.None));
            }

            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/signin", new { Email = email, Password = "a long test password" })).StatusCode);
        }
        finally
        {
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
            foreach (Guid id in storyIds)
            {
                await new StoryRepository(dataSource).DeleteAsync(id, CancellationToken.None);
            }

            if (artistId is Guid artist)
            {
                await new ArtistRepository(dataSource).DeleteAsync(artist, CancellationToken.None);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
