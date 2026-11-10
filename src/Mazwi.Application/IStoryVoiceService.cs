namespace Mazwi.Application;

public interface IStoryVoiceService
{
    Task<StoryDraftDto?> GenerateVoiceAsync(Guid id, string language, Guid reviewVersion, CancellationToken cancellationToken);
}
