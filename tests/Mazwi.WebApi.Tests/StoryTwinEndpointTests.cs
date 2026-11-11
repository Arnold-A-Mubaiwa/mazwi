using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Mazwi.Domain;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Mazwi.WebApi.Tests;

public sealed class TwinSettingsTests
{
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Mazwi";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static TwinSettings From(string environment, params (string Key, string? Value)[] values) => TwinSettings.From(
        new ConfigurationBuilder().AddInMemoryCollection(values.Select(value => KeyValuePair.Create(value.Key, value.Value))).Build(), new Environment(environment));

    [Fact]
    public void From_NothingConfigured_IsOffWithSafeDefaults()
    {
        // Act
        TwinSettings settings = From("Production", ("OPENAI_API_KEY", "key"));
        // Assert
        Assert.False(settings.Enabled);
        Assert.False(settings.CanRun);
        Assert.Equal(TwinSettings.OpenAi, settings.BaseUri);
        Assert.Equal(TwinThreshold.DefaultValue, settings.Threshold.Value);
        Assert.Equal(TimeSpan.FromSeconds(60), settings.SweepInterval);
        Assert.Equal("text-embedding-3-small", settings.EmbeddingModel);
    }

    [Fact]
    public void From_EnabledWithKey_CanRunWithConfiguredValues()
    {
        // Act
        TwinSettings settings = From("Production", ("Mazwi:Twins:Enabled", "true"), ("OPENAI_API_KEY", "key"), ("Mazwi:Twins:Threshold", "0.72"),
            ("Mazwi:Twins:SweepSeconds", "5"), ("MAZWI_EMBEDDING_MODEL", "embed"));
        // Assert
        Assert.True(settings.CanRun);
        Assert.Equal("key", settings.ApiKey);
        Assert.Equal(0.72, settings.Threshold.Value);
        Assert.Equal(TimeSpan.FromSeconds(5), settings.SweepInterval);
        Assert.Equal("embed", settings.EmbeddingModel);
        Assert.False(From("Production", ("Mazwi:Twins:Enabled", "true")).CanRun);
        Assert.Equal(TimeSpan.FromSeconds(60), From("Production", ("Mazwi:Twins:SweepSeconds", "0")).SweepInterval);
    }

    [Fact]
    public void From_InvalidThreshold_FailsAtStartup() =>
        Assert.Throws<DomainException>(() => From("Production", ("Mazwi:Twins:Threshold", "1.5")));

    [Theory]
    [InlineData("Testing", "http://127.0.0.1:5099/x", "http://127.0.0.1:5099/")]
    [InlineData("Testing", "http://localhost:5099", "http://localhost:5099/")]
    [InlineData("Production", "http://127.0.0.1:5099", "https://api.openai.com/")]
    [InlineData("Development", "http://127.0.0.1:5099", "https://api.openai.com/")]
    [InlineData("Testing", "http://example.com", "https://api.openai.com/")]
    [InlineData("Testing", "https://127.0.0.1:5099", "https://api.openai.com/")]
    [InlineData("Testing", "not a url", "https://api.openai.com/")]
    public void From_BaseUrl_OnlyHonouredForLoopbackInTesting(string environment, string configured, string expected) =>
        Assert.Equal(new Uri(expected), From(environment, ("Mazwi:Twins:OpenAiBaseUrl", configured)).BaseUri);
}

public sealed class StoryTwinEndpointTests
{
    private sealed class FakeFingerprinter(string marker) : IStoryFingerprinter
    {
        // This test's stories share one plot; other stories in the shared test database get random, unrelated vectors.
        public Task<StoryFingerprint> FingerprintAsync(string text, string language, string storyType, CancellationToken cancellationToken)
        {
            float[] vector = text.Contains(marker, StringComparison.Ordinal)
                ? [1f, .. new float[63]]
                : Enumerable.Range(0, 64).Select(_ => (float)(Random.Shared.NextDouble() - 0.5)).ToArray();
            return Task.FromResult(new StoryFingerprint("PRIVATE GIST: Tsuro tricks Gudo.", vector));
        }
    }

    [Fact]
    public async Task Twins_SignedOut_Returns401()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-Artist", "none");
        // Act / Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/studio/twins")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync($"/api/stories/{Guid.NewGuid()}/twins/{Guid.NewGuid()}/confirm", null)).StatusCode);
    }

    [Fact]
    public async Task SuggestConfirmRejectAndOptOut_RealPostgres_WorksForOwnersOnly()
    {
        // Arrange
        string connectionString = System.Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string root = Path.Combine(Path.GetTempPath(), "mazwi-twin-tests", Guid.NewGuid().ToString("N"));
        string marker = Guid.NewGuid().ToString("N");
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mazwi"] = connectionString, ["MediaStorage:Root"] = root, ["Mazwi:AccountRequestsPerMinute"] = "100" }));
            builder.ConfigureTestServices(services => services.AddSingleton<IStoryFingerprinter>(new FakeFingerprinter(marker)));
        });
        using HttpClient shona = factory.CreateClient();
        using HttpClient yoruba = factory.CreateClient();
        var artists = new List<Guid>();
        var stories = new List<Guid>();
        try
        {
            artists.Add(await SignUpAsync(shona));
            artists.Add(await SignUpAsync(yoruba));
            Guid mine = await PublishAsync(shona, "sn", $"Tsuro na Gudo {marker}");
            Guid theirs = await PublishAsync(yoruba, "yo", $"Ìjàpá {marker}");
            stories.AddRange([mine, theirs]);
            // Act: run the sweep directly until both new stories have fingerprints.
            await using (AsyncServiceScope scope = factory.Services.CreateAsyncScope())
            {
                IStoryTwinService service = scope.ServiceProvider.GetRequiredService<IStoryTwinService>();
                while (await service.RefreshAsync(100, CancellationToken.None) > 0)
                {
                }
            }

            HttpResponseMessage listed = await shona.GetAsync("/api/studio/twins");
            string raw = await listed.Content.ReadAsStringAsync();
            StoryTwinDto twin = Assert.Single((await listed.Content.ReadFromJsonAsync<StoryTwinDto[]>())!, item => item.TwinStoryId == theirs);
            // Assert
            Assert.Equal("no-store", listed.Headers.CacheControl?.ToString());
            Assert.DoesNotContain("PRIVATE GIST", raw);
            Assert.DoesNotContain("similarity", raw, StringComparison.OrdinalIgnoreCase);
            Assert.Equal((mine, "yo", "suggested"), (twin.StoryId, twin.TwinLanguage, twin.Status));
            Assert.Contains((await yoruba.GetFromJsonAsync<StoryTwinDto[]>("/api/studio/twins"))!, item => item.StoryId == theirs && item.TwinStoryId == mine);

            Assert.Equal(HttpStatusCode.Forbidden, (await yoruba.PostAsync($"/api/stories/{mine}/twins/{theirs}/confirm", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await shona.PostAsync($"/api/stories/{mine}/twins/{Guid.NewGuid()}/confirm", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, (await yoruba.PostAsync($"/api/stories/{theirs}/twins/{mine}/confirm", null)).StatusCode);
            Assert.Equal("confirmed", (await shona.GetFromJsonAsync<StoryTwinDto[]>("/api/studio/twins"))!.Single(item => item.TwinStoryId == theirs).Status);

            // Opting out hides the twin for both artists; opting in brings it back.
            Assert.Equal(HttpStatusCode.BadRequest, (await yoruba.PutAsJsonAsync($"/api/stories/{theirs}/twinning", new { })).StatusCode);
            HttpResponseMessage optOut = await yoruba.PutAsJsonAsync($"/api/stories/{theirs}/twinning", new TwinsAllowedRequest(false));
            Assert.False((await optOut.Content.ReadFromJsonAsync<StoryDraftResponse>())!.TwinsAllowed);
            Assert.DoesNotContain((await shona.GetFromJsonAsync<StoryTwinDto[]>("/api/studio/twins"))!, item => item.TwinStoryId == theirs);
            Assert.False((await yoruba.GetFromJsonAsync<StoryDraftResponse>($"/api/stories/{theirs}/drafts"))!.TwinsAllowed);
            Assert.Equal(HttpStatusCode.Forbidden, (await shona.PutAsJsonAsync($"/api/stories/{theirs}/twinning", new TwinsAllowedRequest(true))).StatusCode);
            await yoruba.PutAsJsonAsync($"/api/stories/{theirs}/twinning", new TwinsAllowedRequest(true));

            // Unlinking is final, even after another sweep.
            Assert.Equal(HttpStatusCode.NoContent, (await shona.PostAsync($"/api/stories/{mine}/twins/{theirs}/reject", null)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await shona.PostAsync($"/api/stories/{mine}/twins/{theirs}/confirm", null)).StatusCode);
            Assert.DoesNotContain((await shona.GetFromJsonAsync<StoryTwinDto[]>("/api/studio/twins"))!, item => item.TwinStoryId == theirs);
        }
        finally
        {
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
            foreach (Guid id in stories)
            {
                await new StoryRepository(dataSource).DeleteAsync(id, CancellationToken.None);
            }

            foreach (Guid id in artists)
            {
                await new ArtistRepository(dataSource).DeleteAsync(id, CancellationToken.None);
            }

            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private static async Task<Guid> SignUpAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/account/signup",
            new { Email = $"twins-{Guid.NewGuid():N}@mazwi.test", Password = "a long test password" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ArtistResponse>())!.Id;
    }

    private static async Task<Guid> PublishAsync(HttpClient client, string language, string title)
    {
        using MultipartFormDataContent form = StoriesEndpointTests.Form(title: title, language: language);
        StoryResponse story = (await (await client.PostAsync("/api/stories", form)).Content.ReadFromJsonAsync<StoryResponse>())!;
        string drafts = $"/api/stories/{story.Id}/drafts";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(drafts + "/transcript", new { Text = $"Kare kare {title}" })).StatusCode);
        StoryDraftResponse draft = (await client.GetFromJsonAsync<StoryDraftResponse>(drafts))!;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/{language}", new ApproveStoryRequest(draft.ReviewVersion))).StatusCode);
        return story.Id;
    }
}
