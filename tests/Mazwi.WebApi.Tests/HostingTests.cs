using System.Net;
using System.Net.Http.Json;
using Mazwi.Application;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace Mazwi.WebApi.Tests;

public sealed class HostingTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void From_Local_NothingIsSwitchedOn()
    {
        // Act
        HostingSettings settings = HostingSettings.From(Config([]));
        // Assert
        Assert.False(settings.Hosted);
        Assert.Empty(settings.TrustedProxies);
    }

    [Fact]
    public void From_HostedWithProxiesAndHttpsSite_ParsesProxies()
    {
        // Act
        HostingSettings settings = HostingSettings.From(Config(new() { ["Mazwi:Hosted"] = "true", ["Mazwi:TrustedProxies"] = " 127.0.0.1 , ::1", ["Mazwi:PublicBaseUrl"] = "https://mazwi.example" }));
        // Assert
        Assert.True(settings.Hosted);
        Assert.Equal([IPAddress.Loopback, IPAddress.IPv6Loopback], settings.TrustedProxies);
    }

    [Theory]
    [InlineData("true", "", "https://mazwi.example", "TrustedProxies")]
    [InlineData("true", "127.0.0.1", "http://mazwi.example", "PublicBaseUrl")]
    [InlineData("true", "127.0.0.1", null, "PublicBaseUrl")]
    [InlineData("false", "not-an-ip", null, "not an IP address")]
    public void From_InvalidHostingSettings_FailsAtStartup(string hosted, string proxies, string? site, string message)
    {
        // Act / Assert
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => HostingSettings.From(Config(new()
        {
            ["Mazwi:Hosted"] = hosted,
            ["Mazwi:TrustedProxies"] = proxies,
            ["Mazwi:PublicBaseUrl"] = site,
        })));
        Assert.Contains(message, failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_TrustedProxies_ReplaceTheLoopbackDefaults()
    {
        // Arrange
        var options = new ForwardedHeadersOptions();
        // Act
        new HostingSettings(true, [IPAddress.Parse("10.0.0.5")]).Apply(options);
        // Assert
        Assert.Equal([IPAddress.Parse("10.0.0.5")], options.KnownProxies);
        Assert.Empty(options.KnownIPNetworks);
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(1, options.ForwardLimit);
    }

    // The test server has no network connection, so a startup filter plays the proxy's address.
    private sealed class FromAddress(IPAddress address) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, call) => { context.Connection.RemoteIpAddress = address; return call(context); });
            next(app);
        };
    }

    private static WebApplicationFactory<Program> Factory(bool hosted, string proxyAddress, IArtistAccountService accounts) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mazwi:Hosted"] = hosted ? "true" : "false",
                ["Mazwi:TrustedProxies"] = hosted ? "127.0.0.1" : "",
                ["Mazwi:PublicBaseUrl"] = "https://mazwi.example",
                ["Mazwi:AccountRequestsPerMinute"] = "2",
            }));
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new FromAddress(IPAddress.Parse(proxyAddress)));
                services.RemoveAll<IArtistAccountService>();
                services.AddSingleton(accounts);
            });
        });

    private static HttpRequestMessage SignIn(string? client, bool https = true)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/account/signin") { Content = JsonContent.Create(new { email = "rudo@example.com", password = "long password" }) };
        request.Headers.Host = "mazwi.example";
        if (client is not null)
        {
            request.Headers.Add("X-Forwarded-For", client);
        }

        request.Headers.Add("X-Forwarded-Proto", https ? "https" : "http");
        return request;
    }

    private static IArtistAccountService Accounts()
    {
        IArtistAccountService accounts = Substitute.For<IArtistAccountService>();
        accounts.SignInAsync("rudo@example.com", "long password", Arg.Any<CancellationToken>()).Returns(new ArtistDto(Guid.NewGuid(), "rudo@example.com"));
        return accounts;
    }

    [Fact]
    public async Task Hosted_TrustedProxy_RateLimitsEachRealClientSeparately()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(hosted: true, "127.0.0.1", Accounts());
        using HttpClient client = factory.CreateClient();
        // Act
        await client.SendAsync(SignIn("203.0.113.1"));
        await client.SendAsync(SignIn("203.0.113.1"));
        HttpResponseMessage third = await client.SendAsync(SignIn("203.0.113.1"));
        HttpResponseMessage someoneElse = await client.SendAsync(SignIn("203.0.113.2"));
        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
        Assert.Equal(HttpStatusCode.OK, someoneElse.StatusCode);
    }

    [Fact]
    public async Task Hosted_UntrustedSender_ForwardedClientIsIgnoredSoEveryoneSharesItsLimit()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(hosted: true, "198.51.100.7", Accounts());
        using HttpClient client = factory.CreateClient();
        // Act
        await client.SendAsync(SignIn("203.0.113.1"));
        await client.SendAsync(SignIn("203.0.113.2"));
        HttpResponseMessage spoofed = await client.SendAsync(SignIn("203.0.113.3"));
        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, spoofed.StatusCode);
    }

    [Fact]
    public async Task Hosted_HttpsFromProxy_SecureCookieAndHsts()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(hosted: true, "127.0.0.1", Accounts());
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.SendAsync(SignIn("203.0.113.9"));
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("secure", Assert.Single(response.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=31536000", Assert.Single(response.Headers.GetValues("Strict-Transport-Security")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Local_HttpRequest_NoSecureCookieNoHstsAndForwardedHeadersIgnored()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory(hosted: false, "127.0.0.1", Accounts());
        using HttpClient client = factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.SendAsync(SignIn("203.0.113.9"));
        HttpResponseMessage second = await client.SendAsync(SignIn("203.0.113.10"));
        HttpResponseMessage third = await client.SendAsync(SignIn("203.0.113.11"));
        // Assert
        Assert.DoesNotContain("secure", Assert.Single(response.Headers.GetValues("Set-Cookie")), StringComparison.OrdinalIgnoreCase);
        Assert.False(response.Headers.Contains("Strict-Transport-Security"));
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }
}
