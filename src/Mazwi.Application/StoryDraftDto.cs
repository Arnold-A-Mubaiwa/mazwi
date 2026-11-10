using Mazwi.Domain;

namespace Mazwi.Application;

public sealed record StoryDraftDto(Guid Id, string Title, string ArtistName, string Language, string Status,
    string? Transcript, IReadOnlyList<TranslationDraftDto> Translations)
{
    public static StoryDraftDto From(Story story) => new(story.Id, story.Details.Title, story.Details.ArtistName,
        story.Details.Language, story.Status, story.Transcript?.Value,
        story.Translations.OrderBy(draft => draft.Language)
            .Select(draft => new TranslationDraftDto(draft.Language, draft.Text.Value, draft.Status)).ToArray());
}
