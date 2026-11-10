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

public sealed class StoryDraftsEndpointTests : IDisposable
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly Story _story = Story.Upload(new StoryDetails("Tsuro", "Rudo", "Shona", "ZW", "folktale", "sn"), new StoryRecording("clip.wav", 12), true);
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public StoryDraftsEndpointTests()
    {
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStoryRepository>();
                services.AddSingleton(_repository);
            });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Get_UneditedStory_ReturnsEmptyUnpublishedDraft()
    {
        // Act
        StoryDraftResponse? response = await _client.GetFromJsonAsync<StoryDraftResponse>($"/api/stories/{_story.Id}/drafts");
        // Assert
        Assert.NotNull(response);
        Assert.Null(response.Transcript);
        Assert.Empty(response.Translations);
        Assert.Equal("unpublished", response.Status);
    }

    [Fact]
    public async Task SaveTranscript_ValidText_ReturnsSavedUnpublishedText()
    {
        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/stories/{_story.Id}/drafts/transcript", new { Text = "Tsuro\nna Gudo", Status = "approved" });
        StoryDraftResponse? draft = await response.Content.ReadFromJsonAsync<StoryDraftResponse>();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(draft);
        Assert.Equal("Tsuro\nna Gudo", draft.Transcript);
        Assert.Equal("unpublished", draft.Status);
        await _repository.Received(1).SaveTranscriptAsync(_story, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SaveTranslation_ValidText_ReturnsDraftWithoutApproval()
    {
        // Arrange
        _story.SetTranscript("Original");
        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/stories/{_story.Id}/drafts/translations/xh", new { Text = "Translation", Status = "approved" });
        StoryDraftResponse? draft = await response.Content.ReadFromJsonAsync<StoryDraftResponse>();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(draft);
        Assert.Equal(new TranslationDraftResponse("xh", "Translation", "draft"), Assert.Single(draft.Translations));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("text\0text")]
    public async Task SaveTranscript_InvalidText_ReturnsBadRequestWithoutWrite(string? text)
    {
        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/stories/{_story.Id}/drafts/transcript", new { Text = text });
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _repository.DidNotReceiveWithAnyArgs().SaveTranscriptAsync(default!, default);
    }

    [Fact]
    public async Task SaveTranscript_OverlongText_ReturnsBadRequest()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsJsonAsync($"/api/stories/{_story.Id}/drafts/transcript", new { Text = new string('a', 20001) })).StatusCode);
    }

    [Theory]
    [InlineData("sn", true)]
    [InlineData("xx", true)]
    [InlineData("xh", false)]
    public async Task SaveTranslation_InvalidLanguageOrMissingOriginal_ReturnsBadRequest(string language, bool hasTranscript)
    {
        // Arrange
        if (hasTranscript)
        {
            _story.SetTranscript("Original");
        }

        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/stories/{_story.Id}/drafts/translations/{language}", new { Text = "Translation" });
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _repository.DidNotReceiveWithAnyArgs().SaveTranslationAsync(default, default!, default);
    }

    [Fact]
    public async Task AllEndpoints_MissingStory_ReturnNotFound()
    {
        // Arrange
        string route = $"/api/stories/{Guid.NewGuid()}/drafts";
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(route)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(route + "/transcript", new { Text = "Words" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(route + "/translations/xh", new { Text = "Words" })).StatusCode);
    }

    [Fact]
    public async Task SaveTranscript_RepositoryFails_ReturnsSafeError()
    {
        // Arrange
        _repository.SaveTranscriptAsync(default!, default).ThrowsAsyncForAnyArgs(new IOException("sensitive database details"));
        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/stories/{_story.Id}/drafts/transcript", new { Text = "Words" });
        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("sensitive database details", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveTranslation_RepositoryFails_ReturnsSafeError()
    {
        // Arrange
        _story.SetTranscript("Original");
        _repository.SaveTranslationAsync(default, default!, default).ThrowsAsyncForAnyArgs(new IOException("sensitive database details"));
        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync($"/api/stories/{_story.Id}/drafts/translations/xh", new { Text = "Words" });
        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("sensitive database details", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SaveTranscript_MalformedJson_ReturnsBadRequest()
    {
        // Arrange
        using var content = new StringContent("{", System.Text.Encoding.UTF8, "application/json");
        // Act / Assert
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PutAsync($"/api/stories/{_story.Id}/drafts/transcript", content)).StatusCode);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
