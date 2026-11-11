using Npgsql;

namespace Mazwi.WebApi;

// The app and the tests use separate databases. A *_tests database is only for the Testing environment,
// so the app can never write real artists' stories into a database the tests clean up.
public static class DatabaseSafety
{
    public static bool IsTestDatabase(string connectionString) =>
        new NpgsqlConnectionStringBuilder(connectionString).Database?.EndsWith("_tests", StringComparison.Ordinal) == true;

    public static void EnsureAppDatabase(string? connectionString, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        if (!string.IsNullOrWhiteSpace(connectionString) && !environment.IsEnvironment("Testing") && IsTestDatabase(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Mazwi points at a *_tests database. Use a separate development database for the app (see docs/databases.md).");
        }
    }
}
