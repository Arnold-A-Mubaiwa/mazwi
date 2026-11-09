using DbUp;

namespace Mazwi.Repository;

public sealed class DatabaseMigrator(string connectionString)
{
    // DbUp exposes synchronous migration I/O. This explicit startup operation is
    // outside request handling; cancellation is observed before and after DbUp.
    public void Migrate(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = DeployChanges.To.PostgresqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(typeof(DatabaseMigrator).Assembly)
            .JournalToPostgresqlTable("public", "schema_versions")
            .WithTransaction()
            .Build()
            .PerformUpgrade();
        if (!result.Successful)
        {
            throw new InvalidOperationException("Database migration failed.", result.Error);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
