using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Mazwi.Domain;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.WebApi.Tests;

public sealed class SubtitleEndpointTests : IDisposable
{
    private static readonly Guid StoryId = Guid.NewGuid();
    private static readonly Guid Version = Guid.NewGuid();
    private readonly IStorySubtitleService _subtitles = Substitute.For<IStorySubtitleService>();
    private readonly IStoryFeedService _feed = Substitute.For<IStoryFeedService>();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public SubtitleEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.SignInTestArtist();
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IStorySubtitleService>(); services.AddSingleton(_subtitles);
                services.RemoveAll<IStoryFeedService>(); services.AddSingleton(_feed);
            });
        });
        _client = _factory.CreateClient();
    }

    private string Route => $"/api/stories/{StoryId}/subtitles/sn";

    [Fact]
    public async Task Save_ValidTiming_ReturnsDraftWithTiming()
    {
        // Arrange
        _subtitles.SaveTimingAsync(StoryId, "sn", Arg.Is<IReadOnlyList<int>>(starts => starts.SequenceEqual(new[] { 0, 1000 })), 3000, Version, Arg.Any<CancellationToken>())
            .Returns(new StoryDraftDto(StoryId, "Story", "Artist", "sn", "unpublished", "A\nB", [], Guid.NewGuid(), false, ".wav", new SubtitleTimingDto([0, 1000], 3000)));
        // Act
        HttpResponseMessage response = await _client.PutAsJsonAsync(Route, new { startsMs = new[] { 0, 1000 }, endMs = 3000, reviewVersion = Version });
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        StoryDraftResponse? draft = await response.Content.ReadFromJsonAsync<StoryDraftResponse>();
        Assert.Equal([0, 1000], draft!.OriginalTiming!.StartsMs);
    }

    [Fact]
    public async Task Save_MissingStory_Returns404()
    {
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PutAsJsonAsync(Route, new { startsMs = new[] { 0 }, endMs = 10, reviewVersion = Version })).StatusCode);
    }

    [Fact]
    public async Task Save_InvalidTimingOrMissingFields_Returns400()
    {
        // Arrange
        _subtitles.SaveTimingAsync(StoryId, "sn", Arg.Any<IReadOnlyList<int>>(), 5, Version, Arg.Any<CancellationToken>())
            .ThrowsAsync(new DomainException("Each line must start after the line before it."));
        // Act
        HttpResponseMessage invalid = await _client.PutAsJsonAsync(Route, new { startsMs = new[] { 10, 0 }, endMs = 5, reviewVersion = Version });
        HttpResponseMessage missing = await _client.PutAsJsonAsync(Route, new { endMs = 5, reviewVersion = Version });
        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("start after the line before", await invalid.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
    }

    [Fact]
    public async Task Save_StaleVersion_Returns409()
    {
        // Arrange
        _subtitles.SaveTimingAsync(StoryId, "sn", Arg.Any<IReadOnlyList<int>>(), Arg.Any<int>(), Version, Arg.Any<CancellationToken>()).ThrowsAsync(new StoryConflictException());
        // Act / Assert
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync(Route, new { startsMs = new[] { 0 }, endMs = 10, reviewVersion = Version })).StatusCode);
    }

    [Fact]
    public async Task Subtitles_TimedVersion_ReturnsWebVttWithoutCaching()
    {
        // Arrange
        _feed.GetSubtitlesAsync(StoryId, "xh", Version, true, Arg.Any<CancellationToken>()).Returns([new SubtitleCueDto(0, 1500, "Kudala"), new SubtitleCueDto(1500, 3_723_004, "Kwakukho")]);
        // Act
        HttpResponseMessage response = await _client.GetAsync($"/api/feed/{StoryId}/subtitles?language=xh&version={Version}&originalAudio=true");
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/vtt", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("WEBVTT\n\n00:00:00.000 --> 00:00:01.500\nKudala\n\n00:00:01.500 --> 01:02:03.004\nKwakukho\n", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Subtitles_UntimedOrUnapproved_Returns404AndStaleReturns409()
    {
        // Arrange
        _feed.GetSubtitlesAsync(StoryId, "sn", Guid.Empty, false, Arg.Any<CancellationToken>()).ThrowsAsync(new StoryConflictException());
        _feed.GetSubtitlesAsync(StoryId, "sn", Version, false, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubtitleCueDto>?)null);
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/feed/{StoryId}/subtitles?language=sn&version={Version}")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.GetAsync($"/api/feed/{StoryId}/subtitles?language=sn&version={Guid.Empty}")).StatusCode);
    }

    [Fact]
    public void WebVtt_CueTextWithArrowOrCarriageReturn_CannotBreakTheFile()
    {
        // Act
        string vtt = WebVtt.Format([new SubtitleCueDto(0, 10, "a --> b\rc")]);
        // Assert
        Assert.Equal("WEBVTT\n\n00:00:00.000 --> 00:00:00.010\na -> b c\n", vtt);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
