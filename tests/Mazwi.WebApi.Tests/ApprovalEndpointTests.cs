using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Mazwi.WebApi.Tests;

public sealed class ApprovalEndpointTests : IDisposable
{
    private static readonly Guid StoryId = Guid.NewGuid();
    private static readonly Guid Version = Guid.NewGuid();
    private readonly IStoryReviewService _service = Substitute.For<IStoryReviewService>();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public ApprovalEndpointTests()
    {
        var draft = new StoryDraftDto(StoryId, "Story", "Artist", "sn", "published", "Kare kare",
            [new TranslationDraftDto("xh", "Kudala", "approved", SpeakerChecked: true)], Version, true, ".wav");
        _service.ApproveAsync(StoryId, "xh", Version, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(draft);
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services => { services.RemoveAll<IStoryReviewService>(); services.AddSingleton(_service); });
        });
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task Approve_SpeakerChecked_PassesFlagAndReturnsIt()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync($"/api/stories/{StoryId}/approvals/xh", new { reviewVersion = Version, speakerChecked = true });
        StoryDraftResponse? result = await response.Content.ReadFromJsonAsync<StoryDraftResponse>();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(Assert.Single(result!.Translations).SpeakerChecked);
        await _service.Received(1).ApproveAsync(StoryId, "xh", Version, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_OldRequestWithoutFlag_TreatsAsNotChecked()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsJsonAsync($"/api/stories/{StoryId}/approvals/xh", new { reviewVersion = Version });
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _service.Received(1).ApproveAsync(StoryId, "xh", Version, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approve_MissingStory_Returns404()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync($"/api/stories/{Guid.NewGuid()}/approvals/xh", new { reviewVersion = Version })).StatusCode);
    }

    [Fact]
    public async Task Approve_MissingBody_Returns400()
    {
        // Act
        HttpResponseMessage response = await _client.PostAsync($"/api/stories/{StoryId}/approvals/xh", new StringContent("", null, "application/json"));
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _service.DidNotReceiveWithAnyArgs().ApproveAsync(default, default!, default, default, default);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
