using System.Text.Json;
using Mazwi.Application;
using Npgsql;

namespace Mazwi.WebApi;

/// <summary>
/// The judge demo: `--seed-demo &lt;folder&gt;` loads demo/demo.json. It only runs against a database whose name ends in
/// <c>_demo</c> and never when hosted, because the demo passwords are public.
/// </summary>
public static class DemoMode
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static bool Allows(string? connectionString, bool hosted) =>
        !hosted && !string.IsNullOrWhiteSpace(connectionString)
        && new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_demo", StringComparison.Ordinal) == true;

    public static DemoContent Load(string folder)
    {
        string path = Path.Combine(folder, "demo.json");
        return JsonSerializer.Deserialize<DemoContent>(File.ReadAllText(path), Json)
            ?? throw new InvalidOperationException("demo.json is empty.");
    }

    /// <summary>Opens a recording inside the demo folder; paths that leave the folder are refused.</summary>
    public static Stream Open(string folder, DemoStory story)
    {
        ArgumentNullException.ThrowIfNull(story);
        string root = Path.GetFullPath(folder) + Path.DirectorySeparatorChar;
        string path = Path.GetFullPath(Path.Combine(root, story.Recording));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("A demo recording must be inside the demo folder.");
        }

        return File.OpenRead(path);
    }
}
