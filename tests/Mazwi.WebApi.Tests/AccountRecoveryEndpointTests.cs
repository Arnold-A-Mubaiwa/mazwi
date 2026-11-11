using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Mazwi.Application;
using Mazwi.Repository;
using Mazwi.WebApi.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using NSubstitute;

namespace Mazwi.WebApi.Tests;

public sealed partial class AccountRecoveryEndpointTests
{
    private readonly IAccountRecoveryService _service = Substitute.For<IAccountRecoveryService>();

    private WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
    {
        builder.UseEnvironment("Testing");
        builder.SignInTestArtist();
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Mazwi:AccountRequestsPerMinute"] = "100" }));
        builder.ConfigureServices(services => { services.RemoveAll<IAccountRecoveryService>(); services.AddSingleton(_service); });
    });

    [Fact]
    public async Task RequestReset_AnyEmail_Returns202()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/account/password-reset", new { email = "nobody@example.com" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/password-reset", new { })).StatusCode);
        await _service.Received(1).RequestPasswordResetAsync("nobody@example.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ConfirmResetAndVerify_GoodOrBadLink_Return204Or400()
    {
        // Arrange
        _service.ResetPasswordAsync("good", "brand new password", Arg.Any<CancellationToken>()).Returns(true);
        _service.VerifyEmailAsync("good", Arg.Any<CancellationToken>()).Returns(true);
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        // Act / Assert
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/account/password-reset/confirm", new { token = "good", password = "brand new password" })).StatusCode);
        HttpResponseMessage bad = await client.PostAsJsonAsync("/api/account/password-reset/confirm", new { token = "bad", password = "brand new password" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        Assert.Contains("expired or was already used", await bad.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/account/verify-email", new { token = "good" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-email", new { token = "bad" })).StatusCode);
    }

    [Fact]
    public async Task ResendVerification_SignedInOrOut_Returns202Or401()
    {
        // Arrange
        await using WebApplicationFactory<Program> factory = Factory();
        using HttpClient client = factory.CreateClient();
        using var signedOut = new HttpRequestMessage(HttpMethod.Post, "/api/account/verification-email");
        signedOut.Headers.Add(TestArtist.Header, TestArtist.Anonymous);
        // Act / Assert
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsync("/api/account/verification-email", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(signedOut)).StatusCode);
        await _service.Received(1).SendVerificationAsync(TestOwner.Id, Arg.Any<CancellationToken>());
    }

    [GeneratedRegex(@"token=([A-Za-z0-9_\-]+)")]
    private static partial Regex TokenPattern();

    [Fact]
    public async Task SignUpVerifyAndReset_LocalPostgres_LinksWorkOnceAndResetEndsOldSessions()
    {
        // Arrange
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Configure the dedicated test database.");
        if (new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The test database name must end in _tests.");
        }

        new DatabaseMigrator(connectionString).Migrate(CancellationToken.None);
        string outbox = Path.Combine(Path.GetTempPath(), "mazwi-recovery-tests", Guid.NewGuid().ToString("N"));
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Mazwi"] = connectionString,
                ["Mazwi:OutboxPath"] = outbox,
                ["Mazwi:AccountRequestsPerMinute"] = "100",
                ["Mazwi:PublicBaseUrl"] = "https://mazwi.example",
            }));
        });
        using HttpClient client = factory.CreateClient();
        using HttpClient otherDevice = factory.CreateClient();
        string email = $"recovery-{Guid.NewGuid():N}@mazwi.test";
        Guid? artistId = null;
        string LatestToken()
        {
            string latest = Directory.GetFiles(outbox, "*.eml").OrderBy(File.GetCreationTimeUtc).ThenBy(path => path, StringComparer.Ordinal).Last();
            string content = File.ReadAllText(latest);
            Assert.Contains($"To: {email}", content, StringComparison.Ordinal);
            Assert.Contains("https://mazwi.example/", content, StringComparison.Ordinal);
            return TokenPattern().Match(content).Groups[1].Value;
        }

        try
        {
            artistId = (await (await client.PostAsJsonAsync("/api/account/signup", new { Email = email, Password = "a long test password" }))
                .Content.ReadFromJsonAsync<ArtistResponse>())!.Id;
            await otherDevice.PostAsJsonAsync("/api/account/signin", new { Email = email, Password = "a long test password" });
            // Act: verify the email with the link sent at sign-up.
            Assert.False((await client.GetFromJsonAsync<ArtistResponse>("/api/account/me"))!.EmailVerified);
            string verification = LatestToken();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/account/verify-email", new { token = verification })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/verify-email", new { token = verification })).StatusCode);
            Assert.True((await client.GetFromJsonAsync<ArtistResponse>("/api/account/me"))!.EmailVerified);
            // Act: reset the password; the link works once and signs out every session.
            Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/account/password-reset", new { email })).StatusCode);
            string reset = LatestToken();
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/account/password-reset/confirm", new { token = reset, password = "a brand new password" })).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/account/password-reset/confirm", new { token = reset, password = "another password!" })).StatusCode);
            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, (await otherDevice.GetAsync("/api/account/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/account/me")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/account/signin", new { Email = email, Password = "a long test password" })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/account/signin", new { Email = email, Password = "a brand new password" })).StatusCode);
        }
        finally
        {
            await using NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
            if (artistId is Guid id)
            {
                await new ArtistRepository(dataSource).DeleteAsync(id, CancellationToken.None);
            }

            if (Directory.Exists(outbox))
            {
                Directory.Delete(outbox, recursive: true);
            }
        }
    }
}
