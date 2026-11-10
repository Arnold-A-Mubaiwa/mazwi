using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryVoiceService(IStoryRepository repository, IStoryMediaStore mediaStore, ISpeechGenerator generator) : IStoryVoiceService
{
    public async Task<StoryDraftDto?> GenerateVoiceAsync(Guid id, string language, Guid reviewVersion, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        SpeechScript script = story.GetSpeechScript(language, reviewVersion);
        GeneratedSpeech speech = await generator.GenerateAsync(script, language, story.Details.Type, cancellationToken);
        var recording = new TranslatedRecording(Guid.NewGuid(), new StoryRecording("voice.wav", speech.Length), TranslatedRecording.StandardVoice);
        story.AttachGeneratedVoice(language, recording, reviewVersion);
        await using (Stream content = speech.OpenRead())
        {
            await mediaStore.SaveAsync(recording.Id, recording.File, content, cancellationToken);
        }

        try
        {
            await repository.SaveRecordingAsync(story.Id, story.Translations.Single(d => d.Language == language), reviewVersion, cancellationToken);
        }
        catch
        {
            await mediaStore.DeleteAsync(recording.Id, recording.File, CancellationToken.None);
            throw;
        }

        Story? saved = await repository.GetAsync(story.Id, cancellationToken);
        return saved is null ? null : StoryDraftDto.From(saved);
    }
}
