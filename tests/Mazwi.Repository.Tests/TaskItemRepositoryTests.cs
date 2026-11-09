using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository.Tests;

public sealed class TaskItemRepositoryTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    [Theory]
    [InlineData("Normal title")]
    [InlineData("Robert'); DROP TABLE task_items;--")]
    [InlineData("Unicode: café 世界")]
    public async Task AddAsync_ValidItem_RoundTripsThroughPostgres(string title)
    {
        // Arrange
        var repository = new TaskItemRepository(fixture.DataSource);
        TaskItem item = TaskItem.Create(title);
        // Act
        await repository.AddAsync(item, CancellationToken.None);
        TaskItem? loaded = await repository.GetAsync(item.Id, CancellationToken.None);
        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(item.Id, loaded.Id);
        Assert.Equal(item.Title, loaded.Title);
    }

    [Fact]
    public async Task GetAsync_UnknownIdentity_ReturnsNull()
    {
        // Arrange
        var repository = new TaskItemRepository(fixture.DataSource);
        // Act
        TaskItem? item = await repository.GetAsync(Guid.NewGuid(), CancellationToken.None);
        // Assert
        Assert.Null(item);
    }

    [Fact]
    public async Task AddAsync_DuplicateIdentity_RejectsDuplicate()
    {
        // Arrange
        var repository = new TaskItemRepository(fixture.DataSource);
        TaskItem item = TaskItem.Create("Unique");
        await repository.AddAsync(item, CancellationToken.None);
        // Act
        PostgresException error = await Assert.ThrowsAsync<PostgresException>(() => repository.AddAsync(item, CancellationToken.None));
        // Assert
        Assert.Equal(PostgresErrorCodes.UniqueViolation, error.SqlState);
    }

    [Fact]
    public async Task AddAsync_NullItem_ThrowsArgumentNullException()
    {
        // Arrange
        var repository = new TaskItemRepository(fixture.DataSource);
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => repository.AddAsync(null!, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_CancelledToken_PropagatesCancellation()
    {
        // Arrange
        var repository = new TaskItemRepository(fixture.DataSource);
        using var source = new CancellationTokenSource();
        await source.CancelAsync();
        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(Guid.NewGuid(), source.Token));
    }

    [Fact]
    public void Migrate_AlreadyMigratedDatabase_DoesNotReapplyScripts()
    {
        // Act / Assert: a second CREATE TABLE would fail if the journal were ignored.
        fixture.Migrator.Migrate(CancellationToken.None);
    }
}
