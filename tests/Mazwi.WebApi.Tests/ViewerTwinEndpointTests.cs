using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Mazwi.Domain;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Mazwi.WebApi.Tests;

public sealed class ViewerTwinEndpointTests
{
    [Fact]
    public async Task TwinsAndVersionRequests_RealPostgres_ViewerSeesTwinAndArtistSeesCounts()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string root = Path.Combine(Path.GetTempPath(), "mazwi-viewer-twin-tests", Guid.NewGuid().ToString("N"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mazwi"] = connectionString,
                ["MediaStorage:Root"] = root,
                ["Mazwi:AccountRequestsPerMinute"] = "100",
                ["Mazwi:VersionRequestsPerMinute"] = "5",
            }));
        });
        using HttpClient shonaArtist = factory.CreateClient();
        using HttpClient yorubaArtist = factory.CreateClient();
        using HttpClient viewer = factory.CreateClient();
        viewer.DefaultRequestHeaders.Add("X-Test-Artist", "none");
        await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
        var artists = new List<Guid>();
        var stories = new List<Guid>();
        try
        {
            artists.Add(await SignUpAsync(shonaArtist));
            artists.Add(await SignUpAsync(yorubaArtist));
            Guid shona = await PublishAsync(shonaArtist, "sn", "Tsuro na Gudo");
            Guid yoruba = await PublishAsync(yorubaArtist, "yo", "Ìjàpá àti Ọ̀bọ");
            stories.AddRange([shona, yoruba]);
            Assert.Empty((await viewer.GetFromJsonAsync<ViewerTwinDto[]>($"/api/feed/{shona}/twins?language=sn"))!);
            await new StoryTwinRepository(dataSource).AddSuggestionsAsync([StoryTwin.Suggest(shona, yoruba, 0.9)], CancellationToken.None);
            Assert.Empty((await viewer.GetFromJsonAsync<ViewerTwinDto[]>($"/api/feed/{shona}/twins?language=sn"))!);
            Assert.Equal(HttpStatusCode.NoContent, (await shonaArtist.PostAsync($"/api/stories/{shona}/twins/{yoruba}/confirm", null)).StatusCode);
            // Act
            HttpResponseMessage listed = await viewer.GetAsync($"/api/feed/{shona}/twins?language=sn");
            ViewerTwinDto twin = Assert.Single((await listed.Content.ReadFromJsonAsync<ViewerTwinDto[]>())!);
            // Assert
            Assert.Equal(new ViewerTwinDto(yoruba, "Ìjàpá àti Ọ̀bọ", "Rudo", "Shona", "yo", false), twin);
            Assert.Equal("no-store", listed.Headers.CacheControl?.ToString());
            Assert.Equal(shona, Assert.Single((await viewer.GetFromJsonAsync<ViewerTwinDto[]>($"/api/feed/{yoruba}/twins?language=yo"))!).StoryId);
            Assert.Equal(HttpStatusCode.BadRequest, (await viewer.GetAsync($"/api/feed/{shona}/twins?language=de")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await viewer.GetAsync($"/api/feed/{Guid.NewGuid()}/twins?language=sn")).StatusCode);

            // Viewers ask for a Shona version; the Yoruba artist sees the count.
            string requests = $"/api/feed/{yoruba}/version-requests";
            Assert.Equal(HttpStatusCode.Accepted, (await viewer.PostAsJsonAsync(requests, new VersionRequestRequest("sn"))).StatusCode);
            Assert.Equal(HttpStatusCode.Accepted, (await viewer.PostAsJsonAsync(requests, new VersionRequestRequest("sn"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await viewer.PostAsJsonAsync(requests, new VersionRequestRequest("yo"))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await viewer.PostAsJsonAsync(requests, new { language = "xyz" })).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await viewer.PostAsJsonAsync($"/api/feed/{Guid.NewGuid()}/version-requests", new VersionRequestRequest("sn"))).StatusCode);
            Assert.Equal(HttpStatusCode.TooManyRequests, (await viewer.PostAsJsonAsync(requests, new VersionRequestRequest("sn"))).StatusCode);
            Assert.Equal([new VersionRequestDto(yoruba, "sn", 2)], (await yorubaArtist.GetFromJsonAsync<VersionRequestDto[]>("/api/studio/version-requests"))!);
            Assert.Empty((await shonaArtist.GetFromJsonAsync<VersionRequestDto[]>("/api/studio/version-requests"))!);
            Assert.Equal(HttpStatusCode.Unauthorized, (await viewer.GetAsync("/api/studio/version-requests")).StatusCode);

            // The Yoruba artist approves a Shona version: the viewer can hear the twin in Shona and the request is answered.
            string drafts = $"/api/stories/{yoruba}/drafts";
            Assert.Equal(HttpStatusCode.OK, (await yorubaArtist.PutAsJsonAsync(drafts + "/translations/sn", new { Text = "Kare kare" })).StatusCode);
            StoryDraftResponse draft = (await yorubaArtist.GetFromJsonAsync<StoryDraftResponse>(drafts))!;
            Assert.Equal(HttpStatusCode.OK, (await yorubaArtist.PostAsJsonAsync($"/api/stories/{yoruba}/approvals/sn", new ApproveStoryRequest(draft.ReviewVersion))).StatusCode);
            Assert.True(Assert.Single((await viewer.GetFromJsonAsync<ViewerTwinDto[]>($"/api/feed/{shona}/twins?language=sn"))!).AvailableInYourLanguage);
            Assert.Empty((await yorubaArtist.GetFromJsonAsync<VersionRequestDto[]>("/api/studio/version-requests"))!);
        }
        finally
        {
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
            new { Email = $"viewer-twins-{Guid.NewGuid():N}@mazwi.test", Password = "a long test password" });
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ArtistResponse>())!.Id;
    }

    private static async Task<Guid> PublishAsync(HttpClient client, string language, string title)
    {
        using MultipartFormDataContent form = StoriesEndpointTests.Form(title: title, language: language);
        StoryResponse story = (await (await client.PostAsync("/api/stories", form)).Content.ReadFromJsonAsync<StoryResponse>())!;
        string drafts = $"/api/stories/{story.Id}/drafts";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(drafts + "/transcript", new { Text = $"Words of {title}" })).StatusCode);
        StoryDraftResponse draft = (await client.GetFromJsonAsync<StoryDraftResponse>(drafts))!;
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/{language}", new ApproveStoryRequest(draft.ReviewVersion))).StatusCode);
        return story.Id;
    }
}
