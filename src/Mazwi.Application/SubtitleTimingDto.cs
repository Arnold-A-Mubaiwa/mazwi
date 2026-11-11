using Mazwi.Domain;

namespace Mazwi.Application;

public sealed record SubtitleTimingDto(IReadOnlyList<int> StartsMs, int EndMs)
{
    public static SubtitleTimingDto? From(Story story, string language) =>
        story.SubtitleTimings.TryGetValue(language, out SubtitleTiming? timing) ? new(timing.StartsMs, timing.EndMs) : null;
}

public sealed record SubtitleCueDto(int StartMs, int EndMs, string Text)
{
    public static IReadOnlyList<SubtitleCueDto>? From(IReadOnlyList<SubtitleCue>? cues) =>
        cues?.Select(cue => new SubtitleCueDto(cue.StartMs, cue.EndMs, cue.Text)).ToArray();
}
