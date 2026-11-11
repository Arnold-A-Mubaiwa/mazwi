using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Mazwi.WebApi.Tests;

public sealed class StoryAccessEndpointTests
{
    private static readonly Guid StoryId = Guid.NewGuid();

    public static TheoryData<string, string> ProtectedRoutes => new()
    {
        { "GET", $"/api/stories/{StoryId}" },
        { "GET", $"/api/stories/{StoryId}/drafts" },
        { "PUT", $"/api/stories/{StoryId}/drafts/transcript" },
        { "PUT", $"/api/stories/{StoryId}/drafts/translations/xh" },
        { "POST", $"/api/stories/{StoryId}/drafts/translations/xh/ai-draft" },
        { "POST", $"/api/stories/{StoryId}/drafts/translations/xh/ai-voice" },
        { "POST", $"/api/stories/{StoryId}/drafts/transcript/ai-draft" },
        { "POST", $"/api/stories/{StoryId}/approvals/sn" },
        { "GET", $"/api/stories/{StoryId}/recordings/sn" },
    };

    private static WebApplicationFactory<Program> Factory(StoryAccess access, IStoryStudioService? studio = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.SignInTestArtist(access);
            if (studio is not null)
            {
                builder.ConfigureServices(services => { services.RemoveAll<IStoryStudioService>(); services.AddSingleton(studio); });
            }
        });

    private static HttpRequestMessage Request(string method, string route, string? artist = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), route);
        if (method != "GET")
        {
            request.Content = JsonContent.Create(new { text = "Words", reviewVersion = Guid.NewGuid() });
        }

        if (artist is not null)
        {
            request.Headers.Add(TestArtist.Header, artist);
        }

        return request;
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task StoryRoute_SignedOut_Returns401(string method, string route)
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(StoryAccess.Allowed);
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request(method, route, TestArtist.Anonymous))).StatusCode);
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task StoryRoute_OtherArtistsStory_Returns403(string method, string route)
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(StoryAccess.Denied);
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.SendAsync(Request(method, route));
        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("another artist", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task StoryRoute_MissingStory_Returns404(string method, string route)
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(StoryAccess.NotFound);
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Request(method, route))).StatusCode);
    }

    [Fact]
    public async Task Upload_SignedOut_Returns401()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(StoryAccess.Allowed);
        using HttpClient client = factory.CreateClient();
        using MultipartFormDataContent form = StoriesEndpointTests.Form();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/stories") { Content = form };
        request.Headers.Add(TestArtist.Header, TestArtist.Anonymous);
        // Act / Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }

    [Fact]
    public async Task Studio_SignedIn_ListsOnlyTheSignedInArtistsStories()
    {
        // Arrange
        IStoryStudioService studio = Substitute.For<IStoryStudioService>();
        studio.GetPageAsync(Arg.Any<Guid>(), 0, 20, Arg.Any<CancellationToken>()).Returns(new StoryStudioPageDto([], null));
        await using WebApplicationFactory<Program> factory = Factory(StoryAccess.Allowed, studio);
        using HttpClient client = factory.CreateClient();
        Guid artist = Guid.NewGuid();
        // Act
        HttpResponseMessage response = await client.SendAsync(Request("GET", "/api/studio", artist.ToString()));
        HttpResponseMessage signedOut = await client.SendAsync(Request("GET", "/api/studio", TestArtist.Anonymous));
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, signedOut.StatusCode);
        await studio.Received(1).GetPageAsync(artist, 0, 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Feed_SignedOut_StaysPublic()
    {
        // Arrange
        IStoryFeedService feed = Substitute.For<IStoryFeedService>();
        feed.GetPageAsync("sn", 0, 20, Arg.Any<CancellationToken>()).Returns(new StoryFeedPageDto([], null));
        await using WebApplicationFactory<Program> factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureServices(services => { services.RemoveAll<IStoryFeedService>(); services.AddSingleton(feed); });
        });
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/feed")).StatusCode);
    }
}
