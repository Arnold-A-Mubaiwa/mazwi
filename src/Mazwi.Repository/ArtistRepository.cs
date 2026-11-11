using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository;

public sealed class ArtistRepository(NpgsqlDataSource dataSource) : IArtistRepository
{
    public async Task<bool> AddAsync(Artist artist, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artist);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        int inserted = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO artists (id, email, password_hash, created_at, email_verified_at, security_stamp)
            VALUES (@Id, @Email, @PasswordHash, @CreatedAt, @EmailVerifiedAt, @SecurityStamp)
            ON CONFLICT (email) DO NOTHING
            """, new { artist.Id, Email = artist.Email.Value, PasswordHash = artist.PasswordHash.Value, artist.CreatedAt, artist.EmailVerifiedAt, artist.SecurityStamp },
            cancellationToken: cancellationToken));
        return inserted == 1;
    }

    public Task<Artist?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        FindAsync("SELECT id, email, password_hash AS PasswordHash, created_at AS CreatedAt, email_verified_at AS EmailVerifiedAt, security_stamp AS SecurityStamp FROM artists WHERE id = @Id", new { Id = id }, cancellationToken);

    public Task<Artist?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        return FindAsync("SELECT id, email, password_hash AS PasswordHash, created_at AS CreatedAt, email_verified_at AS EmailVerifiedAt, security_stamp AS SecurityStamp FROM artists WHERE email = @Email",
            new { Email = email.Value }, cancellationToken);
    }

    // The token is marked used in the same transaction, so two requests with one link cannot both succeed.
    public async Task SaveSecurityAsync(Artist artist, AccountToken? usedToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(artist);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (usedToken is not null)
        {
            int claimed = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE account_tokens SET used_at = @UsedAt WHERE id = @Id AND artist_id = @ArtistId AND used_at IS NULL AND expires_at > @UsedAt
                """, new { usedToken.Id, ArtistId = artist.Id, usedToken.UsedAt }, transaction, cancellationToken: cancellationToken));
            if (claimed != 1)
            {
                throw new DomainException("This link has expired or was already used. Request a new one.");
            }
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE artists SET password_hash = @PasswordHash, security_stamp = @SecurityStamp, email_verified_at = @EmailVerifiedAt WHERE id = @Id
            """, new { artist.Id, PasswordHash = artist.PasswordHash.Value, artist.SecurityStamp, artist.EmailVerifiedAt }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    // Used by tests to remove the accounts they create; stories they owned become unowned.
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM artists WHERE id = @Id", new { Id = id }, cancellationToken: cancellationToken));
    }

    private async Task<Artist?> FindAsync(string sql, object parameters, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        ArtistRow? row = await connection.QuerySingleOrDefaultAsync<ArtistRow>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
        return row is null ? null : Artist.Rehydrate(row.Id, new EmailAddress(row.Email), new PasswordHash(row.PasswordHash), row.CreatedAt, row.EmailVerifiedAt, row.SecurityStamp);
    }

    private sealed class ArtistRow
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset? EmailVerifiedAt { get; set; }
        public Guid SecurityStamp { get; set; }
    }
}
