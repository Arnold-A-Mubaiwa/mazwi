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

public sealed class AiTranslationEndpointTests : IDisposable
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly ITranslationDrafter _drafter = Substitute.For<ITranslationDrafter>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public AiTranslationEndpointTests()
    {
        _story.SetTranscript("Original"); _story.Approve("sn", _story.ReviewVersion);
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _drafter.DraftAsync(default!, default!, default!, default!, default).ReturnsForAnyArgs(new TranslationDraftResult(new StoryText("Molo"), new TranslationNotes(["Name retained"])));
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.SignInTestArtist(access: null);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStoryRepository>(); services.AddSingleton(_repository);
                services.RemoveAll<ITranslationDrafter>(); services.AddSingleton(_drafter);
            });
        });
        _client = _factory.CreateClient();
    }

    private Task<HttpResponseMessage> Draft(Guid? id = null, string language = "xh", Guid? version = null) =>
        _client.PostAsJsonAsync($"/api/stories/{id ?? _story.Id}/drafts/translations/{language}/ai-draft", new { reviewVersion = version ?? _story.ReviewVersion });

    [Fact]
    public async Task AiDraft_ValidApprovedOriginal_ReturnsEditableUnapprovedDraftAndNotes()
    {
        // Act
        HttpResponseMessage response = await Draft();
        StoryDraftResponse? result = await response.Content.ReadFromJsonAsync<StoryDraftResponse>();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        TranslationDraftResponse translation = Assert.Single(result!.Translations);
        Assert.Equal("Molo", translation.Text); Assert.Equal("draft", translation.Status);
        Assert.Equal("Name retained", Assert.Single(translation.Notes!)); Assert.True(result.OriginalApproved);
    }

    [Fact]
    public async Task AiDraft_MissingStory_Returns404WithoutCallingDrafter()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await Draft(Guid.NewGuid())).StatusCode);
        await _drafter.DidNotReceiveWithAnyArgs().DraftAsync(default!, default!, default!, default!, default);
    }

    [Theory]
    [InlineData("sn")]
    [InlineData("xx")]
    public async Task AiDraft_InvalidTarget_Returns422WithoutCallingDrafter(string language)
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Draft(language: language)).StatusCode);
        await _drafter.DidNotReceiveWithAnyArgs().DraftAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task AiDraft_UnapprovedSource_Returns422WithoutCallingDrafter()
    {
        // Arrange
        _story.SetTranscript("Changed original");
        // Act / Assert
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Draft()).StatusCode);
        await _drafter.DidNotReceiveWithAnyArgs().DraftAsync(default!, default!, default!, default!, default);
    }

    [Fact]
    public async Task AiDraft_StaleVersion_Returns409WithoutCallingDrafter()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.Conflict, (await Draft(version: Guid.NewGuid())).StatusCode);
        await _drafter.DidNotReceiveWithAnyArgs().DraftAsync(default!, default!, default!, default!, default);
    }

    [Theory]
    [InlineData(false, HttpStatusCode.BadGateway)]
    [InlineData(true, HttpStatusCode.GatewayTimeout)]
    public async Task AiDraft_UpstreamFailure_ReturnsSafeErrorAndDoesNotSave(bool timeout, HttpStatusCode expected)
    {
        // Arrange
        _drafter.DraftAsync(default!, default!, default!, default!, default).ThrowsAsyncForAnyArgs(new TranslationDraftUnavailableException(timeout));
        // Act
        HttpResponseMessage response = await Draft();
        // Assert
        Assert.Equal(expected, response.StatusCode);
        Assert.Contains("Your saved draft has not changed", await response.Content.ReadAsStringAsync());
        await _repository.DidNotReceiveWithAnyArgs().SaveAiTranslationAsync(default, default!, default, default);
    }

    public void Dispose() { _client.Dispose(); _factory.Dispose(); }
}
