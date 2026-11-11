using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Mazwi.Domain;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Mazwi.WebApi.Tests;

public sealed class ModerationEndpointTests
{
    private static readonly Guid StoryId = Guid.NewGuid();
    private readonly IModerationService _moderation = Substitute.For<IModerationService>();
    private readonly IStoryReportService _reports = Substitute.For<IStoryReportService>();
    private readonly IArtistAccountService _accounts = Substitute.For<IArtistAccountService>();

    private WebApplicationFactory<Program> Factory(string email, bool verified, int reportsPerMinute = 100)
    {
        _accounts.GetAsync(TestOwner.Id, Arg.Any<CancellationToken>()).Returns(new ArtistDto(TestOwner.Id, email, verified));
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.SignInTestArtist();
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mazwi:Moderators"] = " other@example.com , Mod@Example.com ",
                ["Mazwi:ReportsPerMinute"] = reportsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
            }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IModerationService>(); services.AddSingleton(_moderation);
                services.RemoveAll<IStoryReportService>(); services.AddSingleton(_reports);
                services.RemoveAll<IArtistAccountService>(); services.AddSingleton(_accounts);
            });
        });
    }

    [Fact]
    public async Task Report_ListedReason_Returns202AndPassesReasonAndNote()
    {
        // Arrange
        _reports.ReportAsync(StoryId, ReportReason.Misleading, "Wrong words", Arg.Any<CancellationToken>()).Returns(true);
        await using WebApplicationFactory<Program> factory = Factory("viewer@example.com", true);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/feed/{StoryId}/reports") { Content = JsonContent.Create(new { reason = "misleading", note = "Wrong words" }) };
        request.Headers.Add(TestArtist.Header, TestArtist.Anonymous);
        // Act
        HttpResponseMessage response = await client.SendAsync(request);
        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task Report_UnknownReasonOrStory_Returns400Or404()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory("viewer@example.com", true);
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/feed/{StoryId}/reports", new { reason = "boring" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/feed/{StoryId}/reports", new { reason = "42" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync($"/api/feed/{StoryId}/reports", new { reason = "Other", note = new string('n', 501) })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync($"/api/feed/{StoryId}/reports", new { reason = "Other" })).StatusCode);
    }

    [Fact]
    public async Task Report_TooMany_Returns429()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory("viewer@example.com", true, reportsPerMinute: 1);
        using HttpClient client = factory.CreateClient();
        // Act
        await client.PostAsJsonAsync($"/api/feed/{StoryId}/reports", new { reason = "Other" });
        HttpResponseMessage second = await client.PostAsJsonAsync($"/api/feed/{StoryId}/reports", new { reason = "Other" });
        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task Moderation_ListedVerifiedModerator_CanListHideRestoreAndResolve()
    {
        // Arrange
        _moderation.ListAsync(Arg.Any<CancellationToken>()).Returns([new ReportedStoryDto(StoryId, "Story", "Artist", false, 1, ["Harmful"], [], DateTimeOffset.UtcNow)]);
        _moderation.HideAsync(StoryId, Arg.Any<CancellationToken>()).Returns(true);
        _moderation.RestoreAsync(StoryId, Arg.Any<CancellationToken>()).Returns(true);
        _moderation.ResolveAsync(StoryId, Arg.Any<CancellationToken>()).Returns(true);
        await using WebApplicationFactory<Program> factory = Factory("mod@example.com", true);
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        ReportedStoryDto item = Assert.Single((await client.GetFromJsonAsync<ReportedStoryDto[]>("/api/moderation/reports"))!);
        Assert.Equal(StoryId, item.StoryId);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/moderation/stories/{StoryId}/hide", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/moderation/stories/{StoryId}/restore", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/moderation/stories/{StoryId}/resolve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/moderation/stories/{Guid.NewGuid()}/hide", null)).StatusCode);
        Assert.True((await client.GetFromJsonAsync<ArtistResponse>("/api/account/me"))!.IsModerator);
    }

    [Theory]
    [InlineData("artist@example.com", true)]
    [InlineData("mod@example.com", false)]
    public async Task Moderation_NotListedOrUnverified_Returns403(string email, bool verified)
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(email, verified);
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/moderation/reports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/moderation/stories/{StoryId}/hide", null)).StatusCode);
        Assert.False((await client.GetFromJsonAsync<ArtistResponse>("/api/account/me"))!.IsModerator);
        await _moderation.DidNotReceiveWithAnyArgs().HideAsync(default, default);
    }

    [Fact]
    public async Task Moderation_SignedOut_Returns401()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory("mod@example.com", true);
        using HttpClient client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/moderation/reports");
        request.Headers.Add(TestArtist.Header, TestArtist.Anonymous);
        // Act / Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }
}
