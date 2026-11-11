using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class AccountDeletionService(IArtistRepository artists, IStoryRepository stories, IStoryMediaStore mediaStore, IPasswordHasher hasher)
    : IAccountDeletionService
{
    private const int PageSize = 50;
    // Bounded so a story that keeps changing mid-delete cannot loop forever; the account is then kept and the call fails.
    private const int MaximumPasses = 1_000;

    public async Task<AccountDeletion> DeleteAsync(Guid artistId, string password, CancellationToken cancellationToken)
    {
        Artist? artist = await artists.GetAsync(artistId, cancellationToken);
        if (artist is null)
        {
            return AccountDeletion.NotFound;
        }

        if (!artist.CanSignIn(password, hasher))
        {
            return AccountDeletion.WrongPassword;
        }

        for (int pass = 0; pass < MaximumPasses; pass++)
        {
            IReadOnlyList<Guid> ids = await stories.ListOwnedIdsAsync(artistId, 0, PageSize, cancellationToken);
            if (ids.Count == 0)
            {
                await artists.DeleteAsync(artistId, cancellationToken);
                return AccountDeletion.Deleted;
            }

            foreach (Guid id in ids)
            {
                Story? story = await stories.GetAsync(id, cancellationToken);
                if (story is not null)
                {
                    try
                    {
                        await StoryRemovalService.DeleteStoredAsync(stories, mediaStore, story, story.ReviewVersion, cancellationToken);
                    }
                    catch (StoryConflictException)
                    {
                        // Edited while deleting: it is listed again on the next pass with its new version.
                    }
                }
            }
        }

        throw new InvalidOperationException("The account's stories kept changing while they were being deleted. Try again.");
    }
}
