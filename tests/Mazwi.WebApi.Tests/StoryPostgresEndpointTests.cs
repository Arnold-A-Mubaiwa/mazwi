using System.Net;
using System.Net.Http.Json;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Mazwi.WebApi.Tests;

public sealed class StoryPostgresEndpointTests
{
    [Fact]
    public async Task UploadAndEdit_LocalPostgresAndFilesystem_RoundTripsPrivateRecordingAndDrafts()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string root = Path.Combine(Path.GetTempPath(), "mazwi-endpoint-tests", Guid.NewGuid().ToString("N"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mazwi"] = connectionString, ["MediaStorage:Root"] = root }));
        });
        using HttpClient client = factory.CreateClient();
        Guid? createdId = null;
        try
        {
            using var form = StoriesEndpointTests.Form();
            // Act
            HttpResponseMessage response = await client.PostAsync("/api/stories", form);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            StoryResponse? story = await response.Content.ReadFromJsonAsync<StoryResponse>();
            Assert.NotNull(story);
            createdId = story.Id;
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/feed/{story.Id}?language=xh")).StatusCode);
            StudioPageResponse studio = (await client.GetFromJsonAsync<StudioPageResponse>("/api/studio?limit=50"))!;
            Assert.Contains(studio.Items, item => item.Id == story.Id && item.Status == "unpublished");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/studio?limit=0")).StatusCode);
            StoryResponse? loaded = await client.GetFromJsonAsync<StoryResponse>($"/api/stories/{story.Id}");
            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(story.Id, loaded.Id);
            Assert.Equal(story.Title, loaded.Title);
            Assert.Equal("unpublished", loaded.Status);
            Assert.Equal("RIFF0000WAVEdata0000"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(root, $"{story.Id:N}.wav")));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/media/{story.Id:N}.wav")).StatusCode);

            string draftRoute = $"/api/stories/{story.Id}/drafts";
            HttpResponseMessage transcript = await client.PutAsJsonAsync(draftRoute + "/transcript", new { Text = "Tsuro's words\nSecond line" });
            Assert.Equal(HttpStatusCode.OK, transcript.StatusCode);
            HttpResponseMessage translation = await client.PutAsJsonAsync(draftRoute + "/translations/xh", new { Text = "Manual translation\nSecond line" });
            Assert.Equal(HttpStatusCode.OK, translation.StatusCode);
            StoryDraftResponse? reopened = await client.GetFromJsonAsync<StoryDraftResponse>(draftRoute);
            Assert.NotNull(reopened);
            Assert.Equal("Tsuro's words\nSecond line", reopened.Transcript);
            Assert.Equal("unpublished", reopened.Status);
            Assert.Equal(new TranslationDraftResponse("xh", "Manual translation\nSecond line", "draft"), Assert.Single(reopened.Translations));
            using var ranged = new HttpRequestMessage(HttpMethod.Get, $"/api/stories/{story.Id}/recordings/sn");
            ranged.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(0, 3);
            HttpResponseMessage media = await client.SendAsync(ranged);
            Assert.Equal(HttpStatusCode.PartialContent, media.StatusCode);
            Assert.Equal("RIFF"u8.ToArray(), await media.Content.ReadAsByteArrayAsync());
            Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/sn", new ApproveStoryRequest(Guid.NewGuid()))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/sn", new ApproveStoryRequest(reopened.ReviewVersion))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/xh", new ApproveStoryRequest(reopened.ReviewVersion))).StatusCode);
            StoryViewResponse fallback = (await client.GetFromJsonAsync<StoryViewResponse>($"/api/feed/{story.Id}?language=xh"))!;
            Assert.Equal("xh", fallback.TextLanguage); Assert.Equal("sn", fallback.AudioLanguage);
            Assert.False(fallback.SpeakerChecked);
            Assert.False(Assert.Single((await client.GetFromJsonAsync<StoryDraftResponse>(draftRoute))!.Translations).SpeakerChecked);
            Assert.DoesNotContain(".local", fallback.MediaUrl);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/feed?limit=0")).StatusCode);
            using var translatedForm = new MultipartFormDataContent();
            translatedForm.Add(new StringContent(reopened.ReviewVersion.ToString()), "reviewVersion");
            translatedForm.Add(new StringContent("true"), "permission");
            translatedForm.Add(new ByteArrayContent("RIFF0000WAVEdata0000"u8.ToArray()), "recording", "translated.wav");
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"/api/stories/{story.Id}/recordings/xh", translatedForm)).StatusCode);
            StoryDraftResponse attached = (await client.GetFromJsonAsync<StoryDraftResponse>(draftRoute))!;
            Assert.NotNull(Assert.Single(attached.Translations).RecordingId);
            Assert.Equal("draft", Assert.Single(attached.Translations).Status);
            Assert.Equal("sn", (await client.GetFromJsonAsync<StoryViewResponse>($"/api/feed/{story.Id}?language=xh"))!.TextLanguage);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/stories/{story.Id}/recordings/xh")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/xh", new { reviewVersion = attached.ReviewVersion, speakerChecked = true })).StatusCode);
            StoryViewResponse translated = (await client.GetFromJsonAsync<StoryViewResponse>($"/api/feed/{story.Id}?language=xh"))!;
            Assert.Equal("xh", translated.AudioLanguage); Assert.True(translated.SpeakerChecked);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(translated.MediaUrl)).StatusCode);
            await client.PutAsJsonAsync(draftRoute + "/transcript", new { Text = "Changed original" });
            Assert.False((await client.GetFromJsonAsync<StoryDraftResponse>(draftRoute))!.OriginalApproved);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(translated.MediaUrl)).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/feed/{story.Id}?language=xh")).StatusCode);
        }
        finally
        {
            if (createdId is Guid fixtureId)
            {
                await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
                await new StoryRepository(dataSource).DeleteAsync(fixtureId, CancellationToken.None);
            }
            if (Directory.Exists(root))
            {
                foreach (string file in Directory.GetFiles(root))
                {
                    File.Delete(file);
                }

                Directory.Delete(root);
            }
        }
    }
}
