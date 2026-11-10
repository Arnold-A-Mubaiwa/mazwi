using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryService(IStoryRepository repository, IStoryMediaStore mediaStore) : IStoryService
{
    public async Task<StoryDto> UploadAsync(UploadStoryCommand command, Stream content, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var details = new StoryDetails(command.Title, command.ArtistName, command.Community, command.Country, command.Type, command.Language);
        var recording = new StoryRecording(command.FileName, command.Bytes);
        Story story = Story.Upload(details, recording, command.Permission);
        await mediaStore.SaveAsync(story.Id, recording, content, cancellationToken);
        try
        {
            await repository.AddAsync(story, cancellationToken);
        }
        catch
        {
            // Request cancellation must not prevent compensation for a saved file.
            await mediaStore.DeleteAsync(story.Id, recording, CancellationToken.None);
            throw;
        }

        return StoryDto.From(story);
    }

    public async Task<StoryDto?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        return story is null ? null : StoryDto.From(story);
    }
}
