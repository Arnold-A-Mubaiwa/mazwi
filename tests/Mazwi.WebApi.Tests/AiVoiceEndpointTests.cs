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

public sealed class AiVoiceEndpointTests : IDisposable
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _mediaStore = Substitute.For<IStoryMediaStore>();
    private readonly ISpeechGenerator _generator = Substitute.For<ISpeechGenerator>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true);
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AiVoiceEndpointTests()
    {
        _story.SetTranscript("Original");
        _story.SetTranslation("xh", "Molo");
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _generator.GenerateAsync(default!, default!, default!, default).ReturnsForAnyArgs(new GeneratedSpeech(new byte[SpeechScript.WavHeaderBytes + 4]));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStoryRepository>(); services.AddSingleton(_repository);
                services.RemoveAll<IStoryMediaStore>(); services.AddSingleton(_mediaStore);
                services.RemoveAll<ISpeechGenerator>(); services.AddSingleton(_generator);
            });
        });
        _client = _factory.CreateClient();
    }

    private Task<HttpResponseMessage> Generate(Guid? id = null, string language = "xh", Guid? version = null) =>
        _client.PostAsJsonAsync($"/api/stories/{id ?? _story.Id}/drafts/translations/{language}/ai-voice", new { reviewVersion = version ?? _story.ReviewVersion });

    [Fact]
    public async Task AiVoice_SavedTranslation_ReturnsDraftWithStandardVoiceRecording()
    {
        // Act
        HttpResponseMessage response = await Generate();
        StoryDraftResponse? result = await response.Content.ReadFromJsonAsync<StoryDraftResponse>();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        TranslationDraftResponse translation = Assert.Single(result!.Translations);
        Assert.Equal("standard", translation.RecordingVoice);
        Assert.Equal(".wav", translation.RecordingExtension);
        Assert.Equal("draft", translation.Status);
    }

    [Fact]
    public async Task AiVoice_MissingStory_Returns404WithoutGenerating()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await Generate(Guid.NewGuid())).StatusCode);
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task AiVoice_NoSavedTranslation_Returns422()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Generate(language: "en")).StatusCode);
        await _generator.DidNotReceiveWithAnyArgs().GenerateAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task AiVoice_StaleVersion_Returns409()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.Conflict, (await Generate(version: Guid.NewGuid())).StatusCode);
    }

    [Fact]
    public async Task AiVoice_UpstreamFailure_Returns502WithSafeMessage()
    {
        // Arrange
        _generator.GenerateAsync(default!, default!, default!, default).ThrowsAsyncForAnyArgs(new SpeechUnavailableException());
        // Act
        HttpResponseMessage response = await Generate();
        string body = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Contains("AI voice is unavailable", body);
        await _repository.DidNotReceiveWithAnyArgs().SaveRecordingAsync(default, default!, default, default);
    }

    [Fact]
    public async Task AiVoice_Timeout_Returns504()
    {
        // Arrange
        _generator.GenerateAsync(default!, default!, default!, default).ThrowsAsyncForAnyArgs(new SpeechUnavailableException(timedOut: true));
        // Act / Assert
        Assert.Equal(HttpStatusCode.GatewayTimeout, (await Generate()).StatusCode);
    }

    [Fact]
    public async Task AiVoice_MissingBody_Returns400()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsync($"/api/stories/{_story.Id}/drafts/translations/xh/ai-voice", new StringContent("", null, "application/json"));
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
