using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryDraftService(IStoryRepository repository) : IStoryDraftService
{
    public async Task<StoryDraftDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        return story is null ? null : StoryDraftDto.From(story);
    }

    public async Task<StoryDraftDto?> SaveTranscriptAsync(Guid id, string text, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        story.SetTranscript(text);
        await repository.SaveTranscriptAsync(story, cancellationToken);
        return StoryDraftDto.From(story);
    }

    public async Task<StoryDraftDto?> SaveTranslationAsync(Guid id, string language, string text, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        TranslationDraft draft = story.SetTranslation(language, text);
        await repository.SaveTranslationAsync(id, draft, cancellationToken);
        return StoryDraftDto.From(story);
    }
}
