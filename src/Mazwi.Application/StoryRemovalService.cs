using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryRemovalService(IStoryRepository repository, IStoryMediaStore mediaStore) : IStoryRemovalService
{
    public async Task<StoryDraftDto?> UnpublishAsync(Guid id, Guid reviewVersion, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        story.Unpublish(reviewVersion);
        await repository.SaveUnpublishAsync(story, reviewVersion, cancellationToken);
        return StoryDraftDto.From(story);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid reviewVersion, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return false;
        }

        story.EnsureVersion(reviewVersion);
        await DeleteStoredAsync(repository, mediaStore, story, reviewVersion, cancellationToken);
        return true;
    }

    // Rows first, then files: if the database delete fails nothing is lost; a file left behind is only private clutter.
    internal static async Task DeleteStoredAsync(IStoryRepository repository, IStoryMediaStore mediaStore, Story story, Guid reviewVersion, CancellationToken cancellationToken)
    {
        IReadOnlyList<StoredRecording> files = story.RecordingFiles();
        if (!await repository.DeleteAsync(story.Id, reviewVersion, cancellationToken))
        {
            return;
        }

        foreach (StoredRecording file in files)
        {
            await mediaStore.DeleteAsync(file.MediaId, file.File, CancellationToken.None);
        }
    }
}
