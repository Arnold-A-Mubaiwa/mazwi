using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository;

public sealed class AccountTokenRepository(NpgsqlDataSource dataSource) : IAccountTokenRepository
{
    public async Task ReplaceAsync(AccountToken token, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(token);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Only the newest link of each kind works: earlier unused ones are removed.
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM account_tokens WHERE artist_id = @ArtistId AND purpose = @Purpose AND used_at IS NULL",
            new { token.ArtistId, Purpose = token.Purpose.ToString() }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO account_tokens (id, artist_id, purpose, token_hash, expires_at, used_at) VALUES (@Id, @ArtistId, @Purpose, @Hash, @ExpiresAt, NULL)
            """, new { token.Id, token.ArtistId, Purpose = token.Purpose.ToString(), token.Hash, token.ExpiresAt }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<AccountToken?> FindAsync(string hash, AccountTokenPurpose purpose, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        TokenRow? row = await connection.QuerySingleOrDefaultAsync<TokenRow>(new CommandDefinition("""
            SELECT id, artist_id AS ArtistId, purpose, token_hash AS Hash, expires_at AS ExpiresAt, used_at AS UsedAt
            FROM account_tokens WHERE token_hash = @Hash AND purpose = @Purpose
            """, new { Hash = hash, Purpose = purpose.ToString() }, cancellationToken: cancellationToken));
        return row is null ? null : AccountToken.Rehydrate(row.Id, row.ArtistId, Enum.Parse<AccountTokenPurpose>(row.Purpose), row.Hash, row.ExpiresAt, row.UsedAt);
    }

    private sealed class TokenRow
    {
        public Guid Id { get; set; }
        public Guid ArtistId { get; set; }
        public string Purpose { get; set; } = string.Empty;
        public string Hash { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAt { get; set; }
        public DateTimeOffset? UsedAt { get; set; }
    }
}
