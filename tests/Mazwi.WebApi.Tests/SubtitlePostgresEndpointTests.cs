using System.Net;
using System.Net.Http.Json;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Mazwi.WebApi.Tests;

public sealed class SubtitlePostgresEndpointTests
{
    [Fact]
    public async Task TimeApproveAndWatch_LocalPostgres_FeedReturnsCuesAndWebVtt()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string root = Path.Combine(Path.GetTempPath(), "mazwi-subtitle-tests", Guid.NewGuid().ToString("N"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mazwi"] = connectionString, ["MediaStorage:Root"] = root, ["Mazwi:AccountRequestsPerMinute"] = "100" }));
        });
        using HttpClient client = factory.CreateClient();
        Guid? storyId = null;
        Guid? artistId = null;
        try
        {
            HttpResponseMessage signUp = await client.PostAsJsonAsync("/api/account/signup", new { Email = $"subtitles-{Guid.NewGuid():N}@mazwi.test", Password = "a long test password" });
            artistId = (await signUp.Content.ReadFromJsonAsync<ArtistResponse>())!.Id;
            using MultipartFormDataContent form = StoriesEndpointTests.Form();
            StoryResponse story = (await (await client.PostAsync("/api/stories", form)).Content.ReadFromJsonAsync<StoryResponse>())!;
            storyId = story.Id;
            string drafts = $"/api/stories/{story.Id}/drafts";
            StoryDraftResponse saved = (await (await client.PutAsJsonAsync(drafts + "/transcript", new { Text = "Kare kare\nPane imwe nguva" })).Content.ReadFromJsonAsync<StoryDraftResponse>())!;
            // Act
            HttpResponseMessage mismatch = await client.PutAsJsonAsync($"/api/stories/{story.Id}/subtitles/sn", new { startsMs = new[] { 0 }, endMs = 900, reviewVersion = saved.ReviewVersion });
            HttpResponseMessage timed = await client.PutAsJsonAsync($"/api/stories/{story.Id}/subtitles/sn", new { startsMs = new[] { 0, 1200 }, endMs = 3400, reviewVersion = saved.ReviewVersion });
            StoryDraftResponse withTiming = (await timed.Content.ReadFromJsonAsync<StoryDraftResponse>())!;
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/sn", new ApproveStoryRequest(withTiming.ReviewVersion))).StatusCode);
            StoryViewResponse view = (await client.GetFromJsonAsync<StoryViewResponse>($"/api/feed/{story.Id}?language=sn"))!;
            string vtt = await client.GetStringAsync(view.SubtitlesUrl);
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, mismatch.StatusCode);
            Assert.Equal(HttpStatusCode.OK, timed.StatusCode);
            Assert.Equal([0, 1200], withTiming.OriginalTiming!.StartsMs);
            Assert.Equal("Pane imwe nguva", view.Cues![1].Text);
            Assert.Contains("00:00:01.200 --> 00:00:03.400\nPane imwe nguva", vtt, StringComparison.Ordinal);
            await client.PutAsJsonAsync(drafts + "/transcript", new { Text = "Changed\nwords" });
            Assert.Null((await client.GetFromJsonAsync<StoryDraftResponse>(drafts))!.OriginalTiming);
        }
        finally
        {
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
            if (storyId is Guid id)
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
