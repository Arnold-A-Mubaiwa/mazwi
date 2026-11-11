using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Mazwi.WebApi;

// How Mazwi runs when it is hosted behind a reverse proxy (see docs/hosting.md). Locally nothing here is switched on.
public sealed record HostingSettings(bool Hosted, IReadOnlyList<IPAddress> TrustedProxies)
{
    public static HostingSettings From(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        bool hosted = configuration.GetValue("Mazwi:Hosted", false);
        var proxies = new List<IPAddress>();
        foreach (string value in (configuration["Mazwi:TrustedProxies"] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            proxies.Add(IPAddress.TryParse(value, out IPAddress? address) ? address
                : throw new InvalidOperationException($"Mazwi:TrustedProxies has an entry that is not an IP address: '{value}'."));
        }

        if (hosted && proxies.Count == 0)
        {
            throw new InvalidOperationException("Mazwi:Hosted needs Mazwi:TrustedProxies: the address the reverse proxy (or Next.js in front of the API) connects from.");
        }

        if (hosted && !(Uri.TryCreate(configuration["Mazwi:PublicBaseUrl"], UriKind.Absolute, out Uri? site) && site.Scheme == Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException("Mazwi:Hosted needs Mazwi:PublicBaseUrl set to the site's https:// address, so emailed links are correct.");
        }

        return new HostingSettings(hosted, proxies);
    }

    // Only the listed proxies may say who the client is or that the request came over HTTPS; the defaults (any loopback address) are removed.
    public void Apply(ForwardedHeadersOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (IPAddress proxy in TrustedProxies)
        {
            options.KnownProxies.Add(proxy);
        }

        options.ForwardLimit = 1;
    }
}
