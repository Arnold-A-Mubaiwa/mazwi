namespace Mazwi.Application;

public sealed record StoryViewDto(Guid Id, string Title, string ArtistName, string Community, string Country,
    string Type, string OriginalLanguage, string TextLanguage, string AudioLanguage, string Text,
    string RecordingExtension, Guid ReviewVersion, IReadOnlyList<string> AvailableLanguages, string OriginalRecordingExtension = ".mp4", string? AudioVoice = null, bool SpeakerChecked = true);
