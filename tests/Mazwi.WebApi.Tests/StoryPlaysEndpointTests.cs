using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSubstitute;

namespace Mazwi.WebApi.Tests;

public sealed class StoryPlaysEndpointTests
{
    private static readonly Guid StoryId = Guid.NewGuid();
    private static readonly Guid Version = Guid.NewGuid();
    private readonly IStoryStatsService _service = Substitute.For<IStoryStatsService>();

    private WebApplicationFactory<Program> Factory(int playsPerMinute = 100) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.SignInTestArtist();
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Mazwi:PlaysPerStoryPerMinute"] = playsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        builder.ConfigureServices(services => { services.RemoveAll<IStoryStatsService>(); services.AddSingleton(_service); });
    });

    private static HttpRequestMessage Anonymous(HttpMethod method, string route, object? body = null)
    {
        var request = new HttpRequestMessage(method, route) { Content = body is null ? null : JsonContent.Create(body) };
        request.Headers.Add(TestArtist.Header, TestArtist.Anonymous);
        return request;
    }

    [Fact]
    public async Task Record_ApprovedVersionSignedOut_Returns204()
    {
        // Arrange
        _service.RecordPlayAsync(StoryId, "xh", Version, Arg.Any<CancellationToken>()).Returns(true);
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.SendAsync(Anonymous(HttpMethod.Post, $"/api/feed/{StoryId}/plays", new { language = "xh", version = Version }));
        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _service.Received(1).RecordPlayAsync(StoryId, "xh", Version, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Record_NotCountable_Returns404()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Anonymous(HttpMethod.Post, $"/api/feed/{StoryId}/plays", new { language = "sn", version = Version }))).StatusCode);
    }

    [Fact]
    public async Task Record_InvalidBody_Returns400WithoutCounting()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Anonymous(HttpMethod.Post, $"/api/feed/{StoryId}/plays", new { language = "isiXhosa", version = Version }))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Anonymous(HttpMethod.Post, $"/api/feed/{StoryId}/plays", new { version = Version }))).StatusCode);
        await _service.DidNotReceiveWithAnyArgs().RecordPlayAsync(default, default!, default, default);
    }

    [Fact]
    public async Task Record_TooManyForOneStory_Returns429ButOtherStoriesStillCount()
    {
        // Arrange
        _service.RecordPlayAsync(Arg.Any<Guid>(), "sn", Version, Arg.Any<CancellationToken>()).Returns(true);
        await using WebApplicationFactory<Program> factory = Factory(playsPerMinute: 2);
        using HttpClient client = factory.CreateClient();
        var body = new { language = "sn", version = Version };
        // Act
        await client.SendAsync(Anonymous(HttpMethod.Post, $"/api/feed/{StoryId}/plays", body));
        await client.SendAsync(Anonymous(HttpMethod.Post, $"/api/feed/{StoryId}/plays", body));
        HttpResponseMessage third = await client.SendAsync(Anonymous(HttpMethod.Post, $"/api/feed/{StoryId}/plays", body));
        HttpResponseMessage other = await client.SendAsync(Anonymous(HttpMethod.Post, $"/api/feed/{Guid.NewGuid()}/plays", body));
        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, other.StatusCode);
    }

    [Fact]
    public async Task Stats_SignedInOwner_ReturnsOnlyTheirTotals()
    {
        // Arrange
        Guid artist = Guid.NewGuid();
        _service.GetForOwnerAsync(artist, Arg.Any<CancellationToken>()).Returns([new StoryStatsDto(StoryId, 4, 1, [new LanguagePlaysDto("sn", 4, 1)])]);
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/studio/stats");
        request.Headers.Add(TestArtist.Header, artist.ToString());
        // Act
        HttpResponseMessage response = await client.SendAsync(request);
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        StoryStatsDto stats = Assert.Single((await response.Content.ReadFromJsonAsync<StoryStatsDto[]>())!);
        Assert.Equal(4, stats.Plays);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Anonymous(HttpMethod.Get, "/api/studio/stats"))).StatusCode);
    }

    [Fact]
    public async Task PlayAndStats_LocalPostgres_CountsApprovedPlaysForTheOwnerOnly()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string root = Path.Combine(Path.GetTempPath(), "mazwi-play-tests", Guid.NewGuid().ToString("N"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mazwi"] = connectionString, ["MediaStorage:Root"] = root, ["Mazwi:AccountRequestsPerMinute"] = "100" }));
        });
        using HttpClient owner = factory.CreateClient();
        using HttpClient viewer = factory.CreateClient();
        using HttpClient other = factory.CreateClient();
        var artists = new List<Guid>();
        Guid? storyId = null;
        try
        {
            foreach (HttpClient client in new[] { owner, other })
            {
                HttpResponseMessage signUp = await client.PostAsJsonAsync("/api/account/signup", new { Email = $"plays-{Guid.NewGuid():N}@mazwi.test", Password = "a long test password" });
                artists.Add((await signUp.Content.ReadFromJsonAsync<ArtistResponse>())!.Id);
            }

            using MultipartFormDataContent form = StoriesEndpointTests.Form();
            storyId = (await (await owner.PostAsync("/api/stories", form)).Content.ReadFromJsonAsync<StoryResponse>())!.Id;
            StoryDraftResponse draft = (await (await owner.PutAsJsonAsync($"/api/stories/{storyId}/drafts/transcript", new { Text = "Kare kare" })).Content.ReadFromJsonAsync<StoryDraftResponse>())!;
            // Act
            HttpResponseMessage unapproved = await viewer.PostAsJsonAsync($"/api/feed/{storyId}/plays", new { language = "sn", version = draft.ReviewVersion });
            await owner.PostAsJsonAsync($"/api/stories/{storyId}/approvals/sn", new ApproveStoryRequest(draft.ReviewVersion));
            HttpResponseMessage counted = await viewer.PostAsJsonAsync($"/api/feed/{storyId}/plays", new { language = "xh", version = draft.ReviewVersion });
            await viewer.PostAsJsonAsync($"/api/feed/{storyId}/plays", new { language = "sn", version = draft.ReviewVersion });
            StoryStatsDto[] mine = (await owner.GetFromJsonAsync<StoryStatsDto[]>("/api/studio/stats"))!;
            StoryStatsDto[] theirs = (await other.GetFromJsonAsync<StoryStatsDto[]>("/api/studio/stats"))!;
            // Assert
            Assert.Equal(HttpStatusCode.NotFound, unapproved.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, counted.StatusCode);
            StoryStatsDto stats = Assert.Single(mine, item => item.StoryId == storyId);
            Assert.Equal(2, stats.Plays);
            Assert.Equal(2, stats.PlaysLast7Days);
            Assert.Equal("sn", Assert.Single(stats.Languages).Language);
            Assert.DoesNotContain(theirs, item => item.StoryId == storyId);
        }
        finally
        {
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
            if (storyId is Guid id)
            {
                await new StoryRepository(dataSource).DeleteAsync(id, CancellationToken.None);
            }

            foreach (Guid artist in artists)
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
