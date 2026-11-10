using System.Net;
using System.Net.Http.Json;
using Mazwi.Domain;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.WebApi.Tests;

public sealed class TaskItemsEndpointTests : IDisposable
{
    private readonly ITaskItemRepository _repository = Substitute.For<ITaskItemRepository>();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TaskItemsEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ITaskItemRepository>();
                services.AddSingleton(_repository);
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Create_ValidTitle_ReturnsCreatedWithLocation()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/task-items", new { Title = "  Task  " });
        TaskItemResponse? item = await response.Content.ReadFromJsonAsync<TaskItemResponse>();
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(item);
        Assert.Equal("Task", item.Title);
        Assert.EndsWith($"/api/task-items/{item.Id}", response.Headers.Location?.ToString());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Create_InvalidTitle_ReturnsBadRequest(string? title)
    {
        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/task-items", new { Title = title });
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Create_OversizedTitle_ReturnsDomainValidationError()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/task-items", new { Title = new string('a', 201) });
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Invalid input", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public async Task Create_BoundaryTitleLength_ReturnsCreated(int length)
    {
        // Arrange
        string title = new('a', length);
        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync("/api/task-items", new { Title = title });
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_MalformedJson_ReturnsBadRequest()
    {
        // Arrange
        using var content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/task-items", content);
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ExistingItem_ReturnsOk()
    {
        // Arrange
        TaskItem item = TaskItem.Create("Existing");
        _repository.GetAsync(item.Id, Arg.Any<CancellationToken>()).Returns(item);
        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/task-items/{item.Id}");
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new TaskItemResponse(item.Id, "Existing"), await response.Content.ReadFromJsonAsync<TaskItemResponse>());
    }

    [Fact]
    public async Task Get_MissingItem_ReturnsNotFound()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/task-items/{Guid.NewGuid()}");
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_InvalidGuid_ReturnsNotFound()
    {
        // Act
        HttpResponseMessage response = await _client.GetAsync("/api/task-items/not-a-guid");
        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Get_RepositoryFails_ReturnsSanitizedServerError()
    {
        // Arrange
        _repository.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("sensitive detail"));
        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/task-items/{Guid.NewGuid()}");
        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("sensitive detail", await response.Content.ReadAsStringAsync());
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
