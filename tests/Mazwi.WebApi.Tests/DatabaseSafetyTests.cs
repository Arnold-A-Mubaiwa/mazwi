using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Mazwi.WebApi.Tests;

public sealed class DatabaseSafetyTests
{
    private sealed class Environment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Mazwi";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private const string TestDatabase = "Host=localhost;Database=mazwi_tests;Username=u";
    private const string AppDatabase = "Host=localhost;Database=mazwi;Username=u";

    [Theory]
    [InlineData("Production")]
    [InlineData("Development")]
    public void EnsureAppDatabase_TestDatabaseOutsideTesting_Throws(string environment)
    {
        // Act / Assert
        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() => DatabaseSafety.EnsureAppDatabase(TestDatabase, new Environment(environment)));
        Assert.Contains("separate development database", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureAppDatabase_TestingOrAppDatabaseOrMissing_IsAllowed()
    {
        // Act / Assert
        DatabaseSafety.EnsureAppDatabase(TestDatabase, new Environment("Testing"));
        DatabaseSafety.EnsureAppDatabase(AppDatabase, new Environment("Production"));
        DatabaseSafety.EnsureAppDatabase(null, new Environment("Production"));
        DatabaseSafety.EnsureAppDatabase(" ", new Environment("Production"));
        Assert.Throws<ArgumentNullException>(() => DatabaseSafety.EnsureAppDatabase(AppDatabase, null!));
    }

    [Theory]
    [InlineData("Host=h;Database=mazwi_tests", true)]
    [InlineData("Host=h;Database=mazwi_import_tests", true)]
    [InlineData("Host=h;Database=mazwi", false)]
    [InlineData("Host=h;Database=mazwi_tests_old", false)]
    [InlineData("Host=h", false)]
    public void IsTestDatabase_DatabaseName_EndsWithTestsSuffix(string connectionString, bool expected)
    {
        // Act / Assert
        Assert.Equal(expected, DatabaseSafety.IsTestDatabase(connectionString));
    }
}
