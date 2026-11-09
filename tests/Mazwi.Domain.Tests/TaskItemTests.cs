namespace Mazwi.Domain.Tests;

public sealed class TaskItemTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\n")]
    public void Constructor_BlankTitle_ThrowsDomainException(string? value)
    {
        // Arrange / Act / Assert
        Assert.Throws<DomainException>(() => new TaskTitle(value!));
    }

    [Fact]
    public void Constructor_OversizedTitle_ThrowsDomainException()
    {
        // Arrange
        string value = new('a', TaskTitle.MaximumLength + 1);
        // Act / Assert
        Assert.Throws<DomainException>(() => new TaskTitle(value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(200)]
    public void Constructor_BoundaryLength_AcceptsAndTrims(int length)
    {
        // Arrange
        string value = new('a', length);
        // Act
        var title = new TaskTitle($" {value} ");
        // Assert
        Assert.Equal(value, title.Value);
        Assert.Equal(new TaskTitle(value), title);
    }

    [Fact]
    public void Create_ValidTitle_AssignsUniqueIdentity()
    {
        // Act
        TaskItem first = TaskItem.Create("  First  ");
        TaskItem second = TaskItem.Create("First");
        // Assert
        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("First", first.Title.Value);
    }

    [Fact]
    public void Rehydrate_ExistingIdentity_PreservesIdentity()
    {
        // Arrange
        Guid id = Guid.NewGuid();
        // Act
        TaskItem item = TaskItem.Rehydrate(id, "Existing");
        // Assert
        Assert.Equal(id, item.Id);
    }

    [Fact]
    public void Rehydrate_EmptyIdentity_ThrowsDomainException()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => TaskItem.Rehydrate(Guid.Empty, "Existing"));
    }

    [Fact]
    public void Rehydrate_InvalidTitle_ThrowsDomainException()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => TaskItem.Rehydrate(Guid.NewGuid(), ""));
    }

    [Fact]
    public void Rename_ValidTitle_ChangesTitleWithoutChangingIdentity()
    {
        // Arrange
        TaskItem item = TaskItem.Create("Before");
        Guid id = item.Id;
        // Act
        item.Rename(" After ");
        // Assert
        Assert.Equal("After", item.Title.Value);
        Assert.Equal(id, item.Id);
    }

    [Fact]
    public void Rename_InvalidTitle_PreservesExistingState()
    {
        // Arrange
        TaskItem item = TaskItem.Create("Before");
        // Act / Assert
        Assert.Throws<DomainException>(() => item.Rename(""));
        Assert.Equal("Before", item.Title.Value);
    }
}
