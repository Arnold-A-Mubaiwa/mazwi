using System.Net;
using System.Net.Http.Json;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Mazwi.WebApi.Tests;

public sealed class PostgresEndpointTests
{
    [Fact]
    public async Task CreateAndGet_LocalPostgres_RoundTripsThroughAllLayers()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Set MAZWI_TEST_CONNECTION_STRING for the real PostgreSQL endpoint smoke test.");
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (settings.Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The integration database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?> { ["ConnectionStrings:Mazwi"] = connectionString }));
        });
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage created = await client.PostAsJsonAsync("/api/task-items", new { Title = "Database smoke test" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        TaskItemResponse? item = await created.Content.ReadFromJsonAsync<TaskItemResponse>();
        Assert.NotNull(item);
        TaskItemResponse? loaded = await client.GetFromJsonAsync<TaskItemResponse>($"/api/task-items/{item.Id}");
        // Assert
        Assert.Equal(item, loaded);
    }
}
