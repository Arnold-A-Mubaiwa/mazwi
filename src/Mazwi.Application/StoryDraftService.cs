using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryDraftService(IStoryRepository repository, ITranslationDrafter? drafter = null) : IStoryDraftService
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
        return await GetAsync(id, cancellationToken);
    }

    public async Task<StoryDraftDto?> SaveTranslationAsync(Guid id, string language, string text, CancellationToken cancellationToken)
        => await SaveTranslationWithNotesAsync(id, language, text, null, cancellationToken);

    public async Task<StoryDraftDto?> SaveTranslationWithNotesAsync(Guid id, string language, string text, IReadOnlyList<string>? notes, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        TranslationDraft draft = story.SetTranslation(language, text, notes is null ? null : new TranslationNotes(notes));
        await repository.SaveTranslationAsync(id, draft, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<StoryDraftDto?> DraftTranslationAsync(Guid id, string language, Guid expectedVersion, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        string source = story.GetAiTranslationSource(language, expectedVersion);
        if (drafter is null)
        {
            throw new TranslationDraftUnavailableException();
        }

        TranslationDraftResult result = await drafter.DraftAsync(source, story.Details.Language, language, story.Details.Type, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        TranslationDraft draft = story.SetAiTranslation(language, result, expectedVersion);
        await repository.SaveAiTranslationAsync(id, draft, expectedVersion, cancellationToken);
        return await GetAsync(id, cancellationToken);
    }
}
