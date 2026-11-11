using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public NpgsqlDataSource DataSource { get; private set; } = null!;
    public DatabaseMigrator Migrator { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        string connectionString = Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING")
            ?? throw new InvalidOperationException("Set MAZWI_TEST_CONNECTION_STRING for a dedicated local database ending in _tests. Integration tests are never silently skipped.");
        var settings = new NpgsqlConnectionStringBuilder(connectionString);
        if (settings.Database?.EndsWith("_tests", StringComparison.Ordinal) != true)
        {
            throw new InvalidOperationException("The integration database name must end in _tests; do not target development or production data.");
        }

        DataSource = NpgsqlDataSource.Create(connectionString);
        await using NpgsqlConnection connection = await DataSource.OpenConnectionAsync();
        Migrator = new DatabaseMigrator(connectionString);
        Migrator.Migrate(CancellationToken.None);
        // One stable fixture artist owns test stories, so repeated runs do not add accounts to the shared database.
        await new ArtistRepository(DataSource).AddAsync(Artist.Rehydrate(TestOwner.Id, new EmailAddress("repository-tests@mazwi.test"),
            new PasswordHash("fixture-account-cannot-sign-in"), DateTimeOffset.UtcNow), CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (DataSource is not null)
        {
            await DataSource.DisposeAsync();
        }
    }
}
