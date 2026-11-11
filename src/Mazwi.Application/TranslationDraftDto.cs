namespace Mazwi.Application;

public sealed record TranslationDraftDto(string Language, string Text, string Status, Guid? RecordingId = null, string? RecordingExtension = null,
    IReadOnlyList<string>? Notes = null, string? RecordingVoice = null, bool SpeakerChecked = false, SubtitleTimingDto? Timing = null);
