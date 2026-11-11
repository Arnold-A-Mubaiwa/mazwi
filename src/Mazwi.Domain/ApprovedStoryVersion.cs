namespace Mazwi.Domain;

public sealed record ApprovedStoryVersion(string TextLanguage, string AudioLanguage, StoryText Text, Guid MediaId, StoryRecording Recording, string? Voice = null, bool SpeakerChecked = true,
    IReadOnlyList<SubtitleCue>? Cues = null);
