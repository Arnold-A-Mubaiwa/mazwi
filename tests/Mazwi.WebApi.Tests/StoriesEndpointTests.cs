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

public sealed class StoriesEndpointTests : IDisposable
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _media = Substitute.For<IStoryMediaStore>();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public StoriesEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.SignInTestArtist();
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStoryRepository>();
                services.RemoveAll<IStoryMediaStore>();
                services.AddSingleton(_repository);
                services.AddSingleton(_media);
            });
        });
        _client = _factory.CreateClient();
    }

    internal static MultipartFormDataContent Form(string title = "Tsuro", string permission = "true", string fileName = "clip.wav", byte[]? data = null, string language = "sn")
    {
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "title");
        form.Add(new StringContent("Rudo"), "artistName");
        form.Add(new StringContent("Shona"), "community");
        form.Add(new StringContent("ZW"), "country");
        form.Add(new StringContent("folktale"), "type");
        form.Add(new StringContent(language), "language");
        form.Add(new StringContent(permission), "permission");
        form.Add(new ByteArrayContent(data ?? "RIFF0000WAVEdata0000"u8.ToArray()), "recording", fileName);
        return form;
    }

    [Fact]
    public async Task Upload_ValidForm_ReturnsUnpublishedMetadataWithLocation()
    {
        // Arrange
        using var form = Form();
        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/stories", form);
        StoryResponse? story = await response.Content.ReadFromJsonAsync<StoryResponse>();
        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(story);
        Assert.Equal("unpublished", story.Status);
        Assert.Equal("Rudo", story.ArtistName);
        Assert.EndsWith($"/api/stories/{story.Id}", response.Headers.Location?.ToString());
        Assert.DoesNotContain(".local", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "true", "clip.wav")]
    [InlineData("Story", "false", "clip.wav")]
    [InlineData("Story", "true", "clip.exe")]
    public async Task Upload_InvalidForm_DoesNotPersist(string title, string permission, string name)
    {
        // Arrange
        using var form = Form(title, permission, name);
        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/stories", form);
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Upload_MissingRecording_ReturnsBadRequest()
    {
        // Arrange
        using var form = new MultipartFormDataContent { { new StringContent("Story"), "title" } };
        // Act / Assert
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/stories", form)).StatusCode);
    }

    [Fact]
    public async Task Upload_EmptyRecording_ReturnsBadRequest()
    {
        // Arrange
        using var form = Form(data: []);
        // Act / Assert
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsync("/api/stories", form)).StatusCode);
    }

    [Fact]
    public async Task Upload_OversizedRecording_ReturnsClientError()
    {
        // Arrange
        using var form = Form(data: new byte[25_000_001]);
        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/stories", form);
        // Assert
        Assert.Contains(response.StatusCode, new[] { HttpStatusCode.BadRequest, HttpStatusCode.RequestEntityTooLarge });
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Upload_RepositoryFails_ReturnsSafeErrorAndCleansMedia()
    {
        // Arrange
        _repository.AddAsync(default!, default).ThrowsAsyncForAnyArgs(new IOException("private connection password"));
        using var form = Form();
        // Act
        HttpResponseMessage response = await _client.PostAsync("/api/stories", form);
        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("private connection password", await response.Content.ReadAsStringAsync());
        await _media.Received(1).DeleteAsync(Arg.Any<Guid>(), Arg.Any<StoryRecording>(), CancellationToken.None);
    }

    [Fact]
    public async Task Get_ExistingStory_ReturnsMetadata()
    {
        // Arrange
        Story story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
        _repository.GetAsync(story.Id, Arg.Any<CancellationToken>()).Returns(story);
        // Act
        StoryResponse? response = await _client.GetFromJsonAsync<StoryResponse>($"/api/stories/{story.Id}");
        // Assert
        Assert.NotNull(response);
        Assert.Equal(story.Id, response.Id);
        Assert.Equal("unpublished", response.Status);
    }

    [Fact]
    public async Task Get_MissingStory_ReturnsNotFound()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/stories/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Get_PrivateMediaPath_ReturnsNotFound()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/.local/media/clip.wav")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync("/media/clip.wav")).StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
