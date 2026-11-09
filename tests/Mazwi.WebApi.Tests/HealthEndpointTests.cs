using System.Net;
using System.Net.Http.Json;
using Mazwi.WebApi.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mazwi.WebApi.Tests;

public sealed class HealthEndpointTests
{
    [Fact]
    public async Task Get_WithoutDatabaseConfiguration_ReturnsApiLiveness()
    {
        // Arrange
        await using var factory = new WebApplicationFactory<Program>();
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.GetAsync("/api/health");
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new HealthResponse("ok", "Mazwi"), await response.Content.ReadFromJsonAsync<HealthResponse>());
    }
}
