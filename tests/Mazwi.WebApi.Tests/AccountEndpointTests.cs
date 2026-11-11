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
using NSubstitute.ExceptionExtensions;

namespace Mazwi.WebApi.Tests;

public sealed class AccountEndpointTests : IDisposable
{
    private static readonly ArtistDto Artist = new(Guid.NewGuid(), "rudo@example.com");
    private readonly IArtistAccountService _service = Substitute.For<IArtistAccountService>();
    private readonly WebApplicationFactory<Program> _factory;

    public AccountEndpointTests()
    {
        _service.SignUpAsync("rudo@example.com", "long password", Arg.Any<CancellationToken>()).Returns(Artist);
        _service.SignInAsync("rudo@example.com", "long password", Arg.Any<CancellationToken>()).Returns(Artist);
        _service.GetAsync(Artist.Id, Arg.Any<CancellationToken>()).Returns(Artist);
        _factory = Factory(permits: 100);
    }

    private WebApplicationFactory<Program> Factory(int permits) => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["Mazwi:AccountRequestsPerMinute"] = permits.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
        builder.ConfigureServices(services => { services.RemoveAll<IArtistAccountService>(); services.AddSingleton(_service); });
    });

    [Fact]
    public async Task SignUp_NewAccount_ReturnsArtistAndSetsHttpOnlyLaxCookie()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/account/signup", new { email = "rudo@example.com", password = "long password" });
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Artist.Id, (await response.Content.ReadFromJsonAsync<ArtistResponse>())?.Id);
        string cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.StartsWith("mazwi_session=", cookie, StringComparison.Ordinal);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("rudo@example.com", (await client.GetFromJsonAsync<ArtistResponse>("/api/account/me"))?.Email);
    }

    [Fact]
    public async Task SignUp_TakenEmail_Returns409()
    {
        // Arrange
        _service.SignUpAsync("taken@example.com", Arg.Any<string>(), Arg.Any<CancellationToken>()).ThrowsAsync(new AccountExistsException());
        using HttpClient client = _factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsJsonAsync("/api/account/signup", new { email = "taken@example.com", password = "long password" })).StatusCode);
    }

    [Fact]
    public async Task SignUp_InvalidInput_Returns400()
    {
        // Arrange
        _service.SignUpAsync("rudo@example.com", "short", Arg.Any<CancellationToken>()).ThrowsAsync(new DomainException("Choose a password of 10–128 characters."));
        using HttpClient client = _factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/signup", new { email = "rudo@example.com", password = "short" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/signup", new { email = "rudo@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/signup", new { email = new string('a', 255), password = "long password" })).StatusCode);
    }

    [Fact]
    public async Task SignIn_WrongPassword_Returns401WithGenericMessageAndNoCookie()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/account/signin", new { email = "rudo@example.com", password = "wrong password" });
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("do not match an account", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task SignInThenSignOut_ValidAccount_SessionEnds()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        // Act
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/signin", new { email = "rudo@example.com", password = "long password" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/account/signout", null)).StatusCode);
        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task Me_AccountDeletedAfterSignIn_Returns401()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/account/signin", new { email = "rudo@example.com", password = "long password" });
        _service.GetAsync(Artist.Id, Arg.Any<CancellationToken>()).Returns((ArtistDto?)null);
        // Act / Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task Me_SignedOut_Returns401()
    {
        // Arrange
        using HttpClient client = _factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
    }

    [Fact]
    public async Task SignIn_TooManyAttempts_Returns429()
    {
        // Arrange
        await using WebApplicationFactory<Program> limited = Factory(permits: 2);
        using HttpClient client = limited.CreateClient();
        var body = new { email = "rudo@example.com", password = "wrong password" };
        // Act
        await client.PostAsJsonAsync("/api/account/signin", body);
        await client.PostAsJsonAsync("/api/account/signin", body);
        HttpResponseMessage third = await client.PostAsJsonAsync("/api/account/signin", body);
        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, third.StatusCode);
    }

    public void Dispose() => _factory.Dispose();
}
