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

public sealed class AiTranscriptEndpointTests : IDisposable
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _mediaStore = Substitute.For<IStoryMediaStore>();
    private readonly ITranscriber _transcriber = Substitute.For<ITranscriber>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AiTranscriptEndpointTests()
    {
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _mediaStore.OpenReadAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>()).Returns(_ => new MemoryStream([1, 2]));
        _transcriber.TranscribeAsync(default!, default!, default).ReturnsForAnyArgs(new StoryText("Kare kare"));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.SignInTestArtist(access: null);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStoryRepository>(); services.AddSingleton(_repository);
                services.RemoveAll<IStoryMediaStore>(); services.AddSingleton(_mediaStore);
                services.RemoveAll<ITranscriber>(); services.AddSingleton(_transcriber);
            });
        });
        _client = _factory.CreateClient();
    }

    private Task<HttpResponseMessage> Draft(Guid? id = null, Guid? version = null) =>
        _client.PostAsJsonAsync($"/api/stories/{id ?? _story.Id}/drafts/transcript/ai-draft", new { reviewVersion = version ?? _story.ReviewVersion });

    [Fact]
    public async Task AiTranscript_StoredRecording_ReturnsUnapprovedTranscript()
    {
        // Act
        HttpResponseMessage response = await Draft();
        StoryDraftResponse? result = await response.Content.ReadFromJsonAsync<StoryDraftResponse>();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Kare kare", result!.Transcript);
        Assert.False(result.OriginalApproved);
        await _repository.Received(1).SaveAiTranscriptAsync(_story, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AiTranscript_MissingStory_Returns404WithoutTranscribing()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await Draft(Guid.NewGuid())).StatusCode);
        await _transcriber.DidNotReceiveWithAnyArgs().TranscribeAsync(default!, default!, default);
    }

    [Fact]
    public async Task AiTranscript_MissingRecordingFile_Returns422()
    {
        // Arrange
        _mediaStore.OpenReadAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>()).Returns((Stream?)null);
        // Act / Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Draft()).StatusCode);
    }

    [Fact]
    public async Task AiTranscript_StaleVersion_Returns409()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.Conflict, (await Draft(version: Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task AiTranscript_UpstreamFailure_Returns502WithSafeMessage()
    {
        // Arrange
        _transcriber.TranscribeAsync(default!, default!, default).ThrowsAsyncForAnyArgs(new TranscriptionUnavailableException());
        // Act
        HttpResponseMessage response = await Draft();
        string body = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("AI transcription is unavailable", body);
        await _repository.DidNotReceiveWithAnyArgs().SaveAiTranscriptAsync(default!, default, default);
    }

    [Fact]
    public async Task AiTranscript_Timeout_Returns504()
    {
        // Arrange
        _transcriber.TranscribeAsync(default!, default!, default).ThrowsAsyncForAnyArgs(new TranscriptionUnavailableException(timedOut: true));
        // Act / Assert
        Assert.Equal(HttpStatusCode.GatewayTimeout, (await Draft()).StatusCode);
    }

    [Fact]
    public async Task AiTranscript_MissingBody_Returns400()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsync($"/api/stories/{_story.Id}/drafts/transcript/ai-draft", new StringContent("", null, "application/json"));
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
