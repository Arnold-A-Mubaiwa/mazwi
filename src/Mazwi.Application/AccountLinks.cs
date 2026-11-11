namespace Mazwi.Application;

// Emailed links are built from this configured address, never from the request's Host header, which a client controls.
public sealed record AccountLinks
{
    public Uri BaseUrl { get; }

    public AccountLinks(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out Uri? uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("Mazwi:PublicBaseUrl must be an absolute http(s) address without credentials.", nameof(baseUrl));
        }

        BaseUrl = uri;
    }

    public string For(string path, string secret) => new Uri(BaseUrl, $"{path}?token={Uri.EscapeDataString(secret)}").ToString();
}
