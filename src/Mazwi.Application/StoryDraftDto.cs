using Mazwi.Domain;

namespace Mazwi.Application;

public sealed record StoryDraftDto(Guid Id, string Title, string ArtistName, string Language, string Status,
    string? Transcript, IReadOnlyList<TranslationDraftDto> Translations, Guid ReviewVersion = default,
    bool OriginalApproved = false, string RecordingExtension = ".mp4", SubtitleTimingDto? OriginalTiming = null, bool HiddenForReview = false, bool TwinsAllowed = true)
{
    public static StoryDraftDto From(Story story) => new(story.Id, story.Details.Title, story.Details.ArtistName,
        story.Details.Language, story.Status, story.Transcript?.Value,
        story.Translations.OrderBy(draft => draft.Language)
            .Select(draft => new TranslationDraftDto(draft.Language, draft.Text.Value, draft.Status, draft.Recording?.Id, draft.Recording?.File.Extension,
                draft.Notes.Items.Count == 0 ? null : draft.Notes.Items, draft.Recording?.Voice, draft.SpeakerChecked, SubtitleTimingDto.From(story, draft.Language))).ToArray(),
        story.ReviewVersion, story.OriginalApproved, story.Recording.Extension, SubtitleTimingDto.From(story, story.Details.Language), story.HiddenForReview, story.TwinsAllowed);
}
