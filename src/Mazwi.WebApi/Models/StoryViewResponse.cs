using Mazwi.Application;

namespace Mazwi.WebApi.Models;

public sealed record StoryViewResponse(Guid Id, string Title, string ArtistName, string Community, string Country,
    string Type, string OriginalLanguage, string TextLanguage, string AudioLanguage, string Text,
    string RecordingExtension, Guid ReviewVersion, IReadOnlyList<string> AvailableLanguages, string MediaUrl, string OriginalRecordingExtension, string? AudioVoice = null, bool SpeakerChecked = true,
    IReadOnlyList<SubtitleCueDto>? Cues = null, string? SubtitlesUrl = null)
{
    public static StoryViewResponse From(StoryViewDto story)
    {
        string query = $"language={story.TextLanguage}&version={story.ReviewVersion}&originalAudio={story.AudioLanguage == story.OriginalLanguage}";
        return new(story.Id, story.Title, story.ArtistName, story.Community,
            story.Country, story.Type, story.OriginalLanguage, story.TextLanguage, story.AudioLanguage, story.Text,
            story.RecordingExtension, story.ReviewVersion, story.AvailableLanguages,
            $"/api/feed/{story.Id}/recording?{query}", story.OriginalRecordingExtension, story.AudioVoice, story.SpeakerChecked,
            story.Cues, story.Cues is null ? null : $"/api/feed/{story.Id}/subtitles?{query}");
    }
}
