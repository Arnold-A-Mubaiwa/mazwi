using Mazwi.Domain;

namespace Mazwi.WebApi;

/// <summary>
/// Story Twins configuration. The sweep is off unless <c>Mazwi:Twins:Enabled</c> is true and an API key exists,
/// because each published story costs two OpenAI calls (a gist and an embedding).
/// </summary>
public sealed record TwinSettings(bool Enabled, string? ApiKey, Uri BaseUri, TwinThreshold Threshold, TimeSpan SweepInterval,
    string GistModel, string EmbeddingModel)
{
    public static readonly Uri OpenAi = new("https://api.openai.com/");

    public bool CanRun => Enabled && !string.IsNullOrWhiteSpace(ApiKey);

    public static TwinSettings From(IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);
        IConfigurationSection twins = configuration.GetSection("Mazwi:Twins");
        string? apiKey = twins["ApiKey"] ?? configuration["OPENAI_API_KEY"];
        double threshold = double.TryParse(twins["Threshold"], System.Globalization.CultureInfo.InvariantCulture, out double value) ? value : TwinThreshold.DefaultValue;
        int seconds = int.TryParse(twins["SweepSeconds"], out int parsed) && parsed is >= 1 and <= 86_400 ? parsed : 60;
        return new TwinSettings(string.Equals(twins["Enabled"], "true", StringComparison.OrdinalIgnoreCase), apiKey,
            BaseUriFor(twins["OpenAiBaseUrl"], environment), new TwinThreshold(threshold), TimeSpan.FromSeconds(seconds),
            configuration["MAZWI_TRANSLATION_MODEL"] ?? "gpt-6-luna", configuration["MAZWI_EMBEDDING_MODEL"] ?? "text-embedding-3-small");
    }

    /// <summary>A different base URL is honoured only for browser tests: the Testing environment and a loopback address.</summary>
    private static Uri BaseUriFor(string? configured, IHostEnvironment environment) =>
        environment.IsEnvironment("Testing") && Uri.TryCreate(configured, UriKind.Absolute, out Uri? uri) && uri.IsLoopback
            && uri.Scheme == Uri.UriSchemeHttp ? new Uri(uri.GetLeftPart(UriPartial.Authority) + "/") : OpenAi;
}
