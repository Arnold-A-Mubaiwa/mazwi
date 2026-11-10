using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryTranscriptService(IStoryRepository repository, IStoryMediaStore mediaStore, ITranscriber transcriber) : IStoryTranscriptService
{
    public async Task<StoryDraftDto?> DraftTranscriptAsync(Guid id, Guid reviewVersion, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        story.EnsureVersion(reviewVersion);
        StoryText transcript;
        await using (Stream? content = await mediaStore.OpenReadAsync(story.Id, story.Recording, cancellationToken))
        {
            if (content is null)
            {
                throw new AiDraftNotAllowedException("The original recording is missing. Upload the story again before requesting an AI transcript.");
            }

            transcript = await transcriber.TranscribeAsync(content, story.Recording, cancellationToken);
        }

        story.SetAiTranscript(transcript, reviewVersion);
        await repository.SaveAiTranscriptAsync(story, reviewVersion, cancellationToken);
        Story? saved = await repository.GetAsync(story.Id, cancellationToken);
        return saved is null ? null : StoryDraftDto.From(saved);
    }
}
