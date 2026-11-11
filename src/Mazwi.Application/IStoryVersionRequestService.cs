namespace Mazwi.Application;

public interface IStoryVersionRequestService
{
    /// <summary>False when the story is not in the feed. Throws DomainException for a language it cannot be asked for.</summary>
    Task<bool> RequestAsync(Guid storyId, string language, CancellationToken cancellationToken);
    Task<IReadOnlyList<VersionRequestDto>> ListForOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
}
