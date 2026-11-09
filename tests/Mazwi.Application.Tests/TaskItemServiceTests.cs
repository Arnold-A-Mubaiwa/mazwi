using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class TaskItemServiceTests
{
    private readonly ITaskItemRepository _repository = Substitute.For<ITaskItemRepository>();

    [Fact]
    public async Task CreateAsync_ValidTitle_PersistsAggregateAndReturnsDto()
    {
        // Arrange
        var service = new TaskItemService(_repository);
        using var source = new CancellationTokenSource();
        // Act
        TaskItemDto result = await service.CreateAsync(" Title ", source.Token);
        // Assert
        Assert.Equal("Title", result.Title);
        await _repository.Received(1).AddAsync(Arg.Is<TaskItem>(item => item.Id == result.Id && item.Title.Value == "Title"), source.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CreateAsync_InvalidTitle_DoesNotCallRepository(string? title)
    {
        // Arrange
        var service = new TaskItemService(_repository);
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(title!, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task CreateAsync_RepositoryFails_PropagatesFailure()
    {
        // Arrange
        _repository.AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Unavailable"));
        var service = new TaskItemService(_repository);
        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateAsync("Title", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_ExistingItem_ReturnsDtoAndPassesToken()
    {
        // Arrange
        TaskItem item = TaskItem.Create("Found");
        using var source = new CancellationTokenSource();
        _repository.GetAsync(item.Id, source.Token).Returns(item);
        var service = new TaskItemService(_repository);
        // Act
        TaskItemDto? result = await service.GetAsync(item.Id, source.Token);
        // Assert
        Assert.Equal(new TaskItemDto(item.Id, "Found"), result);
        await _repository.Received(1).GetAsync(item.Id, source.Token);
    }

    [Fact]
    public async Task GetAsync_MissingItem_ReturnsNull()
    {
        // Arrange
        var service = new TaskItemService(_repository);
        // Act
        TaskItemDto? result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);
        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_RepositoryFails_PropagatesFailure()
    {
        // Arrange
        _repository.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("Unavailable"));
        var service = new TaskItemService(_repository);
        // Act / Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateAsync_CancelledRepository_PropagatesCancellation()
    {
        // Arrange
        _repository.AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        var service = new TaskItemService(_repository);
        // Act / Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.CreateAsync("Title", CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_CancelledRepository_PropagatesCancellation()
    {
        // Arrange
        _repository.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(new OperationCanceledException());
        var service = new TaskItemService(_repository);
        // Act / Assert
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.GetAsync(Guid.NewGuid(), CancellationToken.None));
    }
}
