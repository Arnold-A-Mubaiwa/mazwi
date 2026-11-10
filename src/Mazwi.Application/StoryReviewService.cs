using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryReviewService(IStoryRepository repository, IStoryMediaStore mediaStore) : IStoryReviewService
{
    public async Task<StoryDraftDto?> ApproveAsync(Guid id, string language, Guid reviewVersion, bool speakerChecked, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        story.Approve(language, reviewVersion, speakerChecked);
        RecordingPlaybackDto? playback = await GetRecordingAsync(id,
            language == story.Details.Language || story.Translations.Single(d => d.Language == language).Recording is not null ? language : story.Details.Language,
            cancellationToken);
        if (playback is null)
        {
            throw new DomainException("The recording is missing. Check the uploaded file before approval.");
        }

        await playback.Content.DisposeAsync();
        await repository.SaveApprovalAsync(id, language, reviewVersion, speakerChecked, cancellationToken);
        Story? saved = await repository.GetAsync(id, cancellationToken);
        return saved is null ? null : StoryDraftDto.From(saved);
    }

    public async Task<StoryDraftDto?> UploadRecordingAsync(UploadTranslationRecordingCommand command, Stream content, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(command.StoryId, cancellationToken);
        if (story is null)
        {
            return null;
        }

        story.EnsureVersion(command.ReviewVersion);
        var recording = new TranslatedRecording(Guid.NewGuid(), new StoryRecording(command.FileName, command.Bytes));
        story.AttachRecording(command.Language, recording, command.Permission);
        await mediaStore.SaveAsync(recording.Id, recording.File, content, cancellationToken);
        try
        {
            await repository.SaveRecordingAsync(story.Id, story.Translations.Single(d => d.Language == command.Language), command.ReviewVersion, cancellationToken);
        }
        catch
        {
            await mediaStore.DeleteAsync(recording.Id, recording.File, CancellationToken.None);
            throw;
        }
        Story? saved = await repository.GetAsync(story.Id, cancellationToken);
        return saved is null ? null : StoryDraftDto.From(saved);
    }

    public async Task<RecordingPlaybackDto?> GetRecordingAsync(Guid id, string language, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        Guid mediaId;
        StoryRecording file;
        if (language == story.Details.Language) { mediaId = story.Id; file = story.Recording; }
        else
        {
            TranslatedRecording? translated = story.Translations.SingleOrDefault(d => d.Language == language)?.Recording;
            if (translated is null)
            {
                return null;
            }

            mediaId = translated.Id; file = translated.File;
        }
        Stream? stream = await mediaStore.OpenReadAsync(mediaId, file, cancellationToken);
        return stream is null ? null : new RecordingPlaybackDto(stream, RecordingPlaybackDto.ContentTypeFor(file.Extension));
    }
}
