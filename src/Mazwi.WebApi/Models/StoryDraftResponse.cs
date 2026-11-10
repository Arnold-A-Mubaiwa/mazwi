using Mazwi.Application;

namespace Mazwi.WebApi.Models;

public sealed record StoryDraftResponse(Guid Id, string Title, string ArtistName, string Language, string Status,
    string? Transcript, IReadOnlyList<TranslationDraftResponse> Translations)
{
    public static StoryDraftResponse From(StoryDraftDto story) => new(story.Id, story.Title, story.ArtistName,
        story.Language, story.Status, story.Transcript,
        story.Translations.Select(draft => new TranslationDraftResponse(draft.Language, draft.Text, draft.Status)).ToArray());
}
