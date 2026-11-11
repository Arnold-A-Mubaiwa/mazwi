using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Mazwi.WebApi.Tests;

public sealed class LanguagesEndpointTests
{
    [Fact]
    public async Task List_SignedOut_ReturnsTheCatalogueWithCaching()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.GetAsync("/api/languages");
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("public, max-age=3600", response.Headers.CacheControl?.ToString());
        LanguageDto[] languages = (await response.Content.ReadFromJsonAsync<LanguageDto[]>())!;
        Assert.Equal(25, languages.Length);
        Assert.Contains(new LanguageDto("am", "Amharic", "አማርኛ", "ሰላም", false), languages);
        Assert.Contains(new LanguageDto("xh", "isiXhosa", "isiXhosa", "Molo", true), languages);
    }

    [Fact]
    public async Task List_PostRequest_IsNotAllowed()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.PostAsync("/api/languages", null);
        // Assert
        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }
}

public sealed class LanguagesPostgresEndpointTests
{
    [Fact]
    public async Task UploadTranslateApproveAndWatch_NewCatalogueLanguages_RoundTripThroughRealPostgres()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string root = Path.Combine(Path.GetTempPath(), "mazwi-language-tests", Guid.NewGuid().ToString("N"));
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
            HttpResponseMessage signup = await client.PostAsJsonAsync("/api/account/signup",
                new { Email = $"languages-{Guid.NewGuid():N}@mazwi.test", Password = "a long test password" });
            artistId = (await signup.Content.ReadFromJsonAsync<ArtistResponse>())!.Id;
            using var unsupported = StoriesEndpointTests.Form(language: "de");
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/stories", unsupported)).StatusCode);
            using var form = StoriesEndpointTests.Form(language: "yo");
            // Act
            StoryResponse story = (await (await client.PostAsync("/api/stories", form)).Content.ReadFromJsonAsync<StoryResponse>())!;
            storyId = story.Id;
            string drafts = $"/api/stories/{story.Id}/drafts";
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(drafts + "/transcript", new { Text = "Ní ayé àtijọ́" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync(drafts + "/translations/am", new { Text = "በጥንት ጊዜ" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync(drafts + "/translations/de", new { Text = "Es war einmal" })).StatusCode);
            StoryDraftResponse draft = (await client.GetFromJsonAsync<StoryDraftResponse>(drafts))!;
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/yo", new ApproveStoryRequest(draft.ReviewVersion))).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync($"/api/stories/{story.Id}/approvals/am", new ApproveStoryRequest(draft.ReviewVersion))).StatusCode);
            StoryViewResponse view = (await client.GetFromJsonAsync<StoryViewResponse>($"/api/feed/{story.Id}?language=am"))!;
            // Assert
            Assert.Equal("yo", view.OriginalLanguage);
            Assert.Equal("am", view.TextLanguage);
            Assert.Equal("yo", view.AudioLanguage);
            Assert.Equal(["am", "yo"], view.AvailableLanguages.Order());
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/feed/{story.Id}?language=de")).StatusCode);
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
