using System.Text.Encodings.Web;
using Mazwi.Application;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Mazwi.WebApi.Tests;

// Signs every request in as TestOwner unless it sends "X-Test-Artist: none" or another artist id.
internal static class TestArtist
{
    public const string Header = "X-Test-Artist";
    public const string Anonymous = "none";

    // access: the owner check result for mocked services; null keeps the real check (for tests that mock the repository).
    public static IWebHostBuilder SignInTestArtist(this IWebHostBuilder builder, StoryAccess? access = StoryAccess.Allowed) =>
        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(TestAuthHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            if (access is StoryAccess result)
            {
                IStoryAccessService service = Substitute.For<IStoryAccessService>();
                service.CheckOwnerAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(result);
                services.RemoveAll<IStoryAccessService>();
                services.AddSingleton(service);
            }
        });

    private sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string? value = Request.Headers[Header];
            if (value == Anonymous)
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Guid artistId = Guid.TryParse(value, out Guid id) ? id : TestOwner.Id;
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(ArtistClaims.For(artistId, "artist@mazwi.test", SchemeName), SchemeName)));
        }
    }
}
