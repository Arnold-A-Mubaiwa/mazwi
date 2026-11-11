namespace Mazwi.Application;

public enum AccountDeletion
{
    Deleted,
    WrongPassword,
    NotFound,
}

public interface IAccountDeletionService
{
    // Deletes the artist and every story they own, with all recordings, after checking their password.
    Task<AccountDeletion> DeleteAsync(Guid artistId, string password, CancellationToken cancellationToken);
}
