using System.Security.Cryptography;
using System.Text;

namespace Mazwi.Domain;

/// <summary>A private English gist of a story's approved words and its embedding. It never leaves the server.</summary>
public sealed record StoryFingerprint
{
    public const int MaxGistLength = 2000;
    public const int MaxDimensions = 4096;

    public string Gist { get; }
    public IReadOnlyList<float> Vector { get; }

    public StoryFingerprint(string gist, IEnumerable<float> vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        string text = gist?.Trim() ?? string.Empty;
        if (text.Length is 0 || text.Length > MaxGistLength || text.Any(c => char.IsControl(c) && c != '\n'))
        {
            throw new DomainException($"A story gist must contain 1–{MaxGistLength} characters.");
        }

        float[] values = vector.ToArray();
        if (values.Length is 0 || values.Length > MaxDimensions || values.Any(value => !float.IsFinite(value)))
        {
            throw new DomainException("A story fingerprint needs a finite vector.");
        }

        double norm = Math.Sqrt(values.Sum(value => (double)value * value));
        if (norm == 0)
        {
            throw new DomainException("A story fingerprint cannot be all zeros.");
        }

        Gist = text;
        Vector = values.Select(value => (float)(value / norm)).ToArray();
    }

    /// <summary>Cosine similarity; 0 when the vectors came from different models (different sizes).</summary>
    public double SimilarityTo(StoryFingerprint other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.Vector.Count != Vector.Count)
        {
            return 0;
        }

        double sum = 0;
        for (int i = 0; i < Vector.Count; i++)
        {
            sum += (double)Vector[i] * other.Vector[i];
        }

        return Math.Clamp(sum, -1, 1);
    }

    /// <summary>Hash of the approved words a fingerprint was made from; a new hash means the fingerprint is stale.</summary>
    public static string SourceHash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
}

/// <summary>A published story that needs a (new) fingerprint.</summary>
public sealed record StoryToFingerprint(Guid StoryId, string Language, string StoryType, string Text)
{
    public string SourceHash => StoryFingerprint.SourceHash(Text);
}

/// <summary>A published, twinnable story with a current fingerprint.</summary>
public sealed record TwinCandidate(Guid StoryId, Guid OwnerId, string Language, StoryFingerprint Fingerprint);
