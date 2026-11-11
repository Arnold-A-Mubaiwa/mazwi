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
            INSERT INTO artists (id, email, password_hash, created_at) VALUES (@Id, @Email, @PasswordHash, @CreatedAt)
            ON CONFLICT (email) DO NOTHING
            """, new { artist.Id, Email = artist.Email.Value, PasswordHash = artist.PasswordHash.Value, artist.CreatedAt },
            cancellationToken: cancellationToken));
        return inserted == 1;
    }

    public Task<Artist?> GetAsync(Guid id, CancellationToken cancellationToken) =>
        FindAsync("SELECT id, email, password_hash AS PasswordHash, created_at AS CreatedAt FROM artists WHERE id = @Id", new { Id = id }, cancellationToken);

    public Task<Artist?> FindByEmailAsync(EmailAddress email, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(email);
        return FindAsync("SELECT id, email, password_hash AS PasswordHash, created_at AS CreatedAt FROM artists WHERE email = @Email",
            new { Email = email.Value }, cancellationToken);
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
        return row is null ? null : Artist.Rehydrate(row.Id, new EmailAddress(row.Email), new PasswordHash(row.PasswordHash), row.CreatedAt);
    }

    private sealed class ArtistRow
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public DateTimeOffset CreatedAt { get; set; }
    }
}
