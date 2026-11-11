namespace Mazwi.Domain;

public interface IStoryVersionRequestRepository
{
    Task AddAsync(VersionRequest request, CancellationToken cancellationToken);
    /// <summary>Totals for the owner's stories, only for languages each story is still not approved in.</summary>
    Task<IReadOnlyList<VersionRequestTotal>> ListForOwnerAsync(Guid ownerId, CancellationToken cancellationToken);
}
