namespace Mazwi.Domain;

public sealed record SubtitleCue(int StartMs, int EndMs, string Text);

// When each line of a saved text starts in its recording, set by the artist while listening.
// Every non-empty line is one cue; a cue ends where the next one starts, and the last ends at EndMs.
public sealed class SubtitleTiming : IEquatable<SubtitleTiming>
{
    public const int MaximumCues = 2_000;
    public const int MaximumEndMs = 4 * 60 * 60 * 1000;
    public IReadOnlyList<int> StartsMs { get; }
    public int EndMs { get; }

    public SubtitleTiming(IEnumerable<int> startsMs, int endMs)
    {
        int[] starts = startsMs?.ToArray() ?? [];
        if (starts.Length is 0 or > MaximumCues)
        {
            throw new DomainException("Time every line of the text before saving subtitles.");
        }

        if (starts[0] < 0 || starts.Zip(starts.Skip(1)).Any(pair => pair.Second <= pair.First))
        {
            throw new DomainException("Each line must start after the line before it.");
        }

        if (endMs <= starts[^1] || endMs > MaximumEndMs)
        {
            throw new DomainException("The subtitles must end after the last line starts, within four hours.");
        }

        StartsMs = starts;
        EndMs = endMs;
    }

    public static IReadOnlyList<string> Lines(StoryText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Value.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).ToArray();
    }

    public bool Fits(StoryText text) => Lines(text).Count == StartsMs.Count;

    public IReadOnlyList<SubtitleCue> Cues(StoryText text)
    {
        IReadOnlyList<string> lines = Lines(text);
        if (lines.Count != StartsMs.Count)
        {
            throw new DomainException($"These subtitles time {StartsMs.Count} lines, but the text has {lines.Count}. Time the text again.");
        }

        return lines.Select((line, index) => new SubtitleCue(StartsMs[index], index + 1 < StartsMs.Count ? StartsMs[index + 1] : EndMs, line)).ToArray();
    }

    public bool Equals(SubtitleTiming? other) => other is not null && EndMs == other.EndMs && StartsMs.SequenceEqual(other.StartsMs);
    public override bool Equals(object? obj) => Equals(obj as SubtitleTiming);
    public override int GetHashCode() => HashCode.Combine(EndMs, StartsMs.Count, StartsMs.Count > 0 ? StartsMs[0] : 0);
}
