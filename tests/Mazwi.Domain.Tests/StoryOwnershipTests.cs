namespace Mazwi.Domain.Tests;

public sealed class StoryOwnershipTests
{
    private static readonly StoryDetails Details = new("Story", "Artist", "Shona", "ZW", "story", "sn");
    private static readonly StoryRecording Recording = new("clip.wav", 12);

    [Fact]
    public void Upload_Owner_IsOwnedOnlyByThatArtist()
    {
        // Act
        Story story = Story.Upload(Details, Recording, true, TestOwner.Id);
        // Assert
        Assert.Equal(TestOwner.Id, story.OwnerId);
        Assert.True(story.IsOwnedBy(TestOwner.Id));
        Assert.False(story.IsOwnedBy(Guid.NewGuid()));
        Assert.False(story.IsOwnedBy(Guid.Empty));
    }

    [Fact]
    public void Upload_EmptyOwner_Throws()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Story.Upload(Details, Recording, true, Guid.Empty));
    }

    [Fact]
    public void EnsureOwnedBy_OtherArtist_ThrowsAccessDenied()
    {
        // Arrange
        Story story = Story.Upload(Details, Recording, true, TestOwner.Id);
        // Act / Assert
        story.EnsureOwnedBy(TestOwner.Id);
        Assert.Throws<StoryAccessDeniedException>(() => story.EnsureOwnedBy(Guid.NewGuid()));
    }

    [Fact]
    public void Rehydrate_LegacyStoryWithoutOwner_IsOwnedByNobody()
    {
        // Act
        Story story = Story.Rehydrate(Guid.NewGuid(), Details, Recording, DateTimeOffset.UtcNow);
        // Assert
        Assert.Null(story.OwnerId);
        Assert.False(story.IsOwnedBy(TestOwner.Id));
        Assert.Throws<StoryAccessDeniedException>(() => story.EnsureOwnedBy(TestOwner.Id));
    }

    [Fact]
    public void Rehydrate_Owner_KeepsOwnerAndRejectsEmptyOwner()
    {
        // Act
        Story story = Story.Rehydrate(Guid.NewGuid(), Details, Recording, DateTimeOffset.UtcNow, ownerId: TestOwner.Id);
        // Assert
        Assert.True(story.IsOwnedBy(TestOwner.Id));
        Assert.Throws<DomainException>(() => Story.Rehydrate(Guid.NewGuid(), Details, Recording, DateTimeOffset.UtcNow, ownerId: Guid.Empty));
    }
}
