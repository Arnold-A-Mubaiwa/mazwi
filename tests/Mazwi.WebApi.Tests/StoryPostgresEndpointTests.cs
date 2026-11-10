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
    public async Task UploadAndGet_LocalPostgresAndFilesystem_RoundTripsPrivateRecording()
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
        try
        {
            using var form = StoriesEndpointTests.Form();
            // Act
            HttpResponseMessage response = await client.PostAsync("/api/stories", form);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            StoryResponse? story = await response.Content.ReadFromJsonAsync<StoryResponse>();
            Assert.NotNull(story);
            StoryResponse? loaded = await client.GetFromJsonAsync<StoryResponse>($"/api/stories/{story.Id}");
            // Assert
            Assert.NotNull(loaded);
            Assert.Equal(story.Id, loaded.Id);
            Assert.Equal(story.Title, loaded.Title);
            Assert.Equal("unpublished", loaded.Status);
            Assert.Equal("RIFF0000WAVEdata0000"u8.ToArray(), await File.ReadAllBytesAsync(Path.Combine(root, $"{story.Id:N}.wav")));
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/media/{story.Id:N}.wav")).StatusCode);
        }
        finally
        {
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
