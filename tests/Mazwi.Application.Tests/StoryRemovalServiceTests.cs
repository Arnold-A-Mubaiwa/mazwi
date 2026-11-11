using Mazwi.Domain;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Mazwi.Application.Tests;

public sealed class StoryRemovalServiceTests
{
    private readonly IStoryRepository _repository = Substitute.For<IStoryRepository>();
    private readonly IStoryMediaStore _media = Substitute.For<IStoryMediaStore>();
    private readonly IArtistRepository _artists = Substitute.For<IArtistRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly Story _story = Story.Upload(new StoryDetails("Story", "Artist", "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 12), true, TestOwner.Id);
    private readonly TranslatedRecording _voice = new(Guid.NewGuid(), new StoryRecording("xh.wav", 9));

    public StoryRemovalServiceTests()
    {
        _story.SetTranscript("Kare kare");
        _story.SetTranslation("xh", "Kudala");
        _story.AttachRecording("xh", _voice, true);
        _repository.GetAsync(_story.Id, Arg.Any<CancellationToken>()).Returns(_story);
        _repository.DeleteAsync(_story.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        _hasher.Verify(Arg.Any<PasswordHash>(), "long password").Returns(true);
    }

    [Fact]
    public async Task UnpublishAsync_PublishedStory_SavesWithExpectedVersion()
    {
        // Arrange
        _story.Approve("sn", _story.ReviewVersion);
        Guid version = _story.ReviewVersion;
        // Act
        StoryDraftDto? result = await new StoryRemovalService(_repository, _media).UnpublishAsync(_story.Id, version, CancellationToken.None);
        // Assert
        Assert.Equal("unpublished", result!.Status);
        await _repository.Received(1).SaveUnpublishAsync(_story, version, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnpublishAsync_MissingOrNotPublished_ReturnsNullOrThrowsWithoutSaving()
    {
        // Arrange
        var service = new StoryRemovalService(_repository, _media);
        // Act / Assert
        Assert.Null(await service.UnpublishAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => service.UnpublishAsync(_story.Id, _story.ReviewVersion, CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().SaveUnpublishAsync(default!, default, default);
    }

    [Fact]
    public async Task DeleteAsync_CurrentVersion_DeletesRowsThenEveryFile()
    {
        // Arrange
        Guid version = _story.ReviewVersion;
        // Act
        bool deleted = await new StoryRemovalService(_repository, _media).DeleteAsync(_story.Id, version, CancellationToken.None);
        // Assert
        Assert.True(deleted);
        Received.InOrder(() =>
        {
            _repository.DeleteAsync(_story.Id, version, Arg.Any<CancellationToken>());
            _media.DeleteAsync(_story.Id, _story.Recording, Arg.Any<CancellationToken>());
            _media.DeleteAsync(_voice.Id, _voice.File, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task DeleteAsync_StaleVersionOrRowDeleteFails_KeepsFiles()
    {
        // Arrange
        var service = new StoryRemovalService(_repository, _media);
        // Act / Assert
        await Assert.ThrowsAsync<StoryConflictException>(() => service.DeleteAsync(_story.Id, Guid.NewGuid(), CancellationToken.None));
        _repository.DeleteAsync(_story.Id, _story.ReviewVersion, Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("database down"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(_story.Id, _story.ReviewVersion, CancellationToken.None));
        await _media.DidNotReceiveWithAnyArgs().DeleteAsync(default, default!, default);
    }

    [Fact]
    public async Task DeleteAsync_MissingStory_ReturnsFalse()
    {
        // Act / Assert
        Assert.False(await new StoryRemovalService(_repository, _media).DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));
    }

    private Artist Owner() => Artist.Rehydrate(TestOwner.Id, new EmailAddress("rudo@example.com"), new PasswordHash("hash"), DateTimeOffset.UtcNow);

    [Fact]
    public async Task DeleteAccountAsync_CorrectPassword_DeletesEveryStoryAndFilesThenTheArtist()
    {
        // Arrange
        _artists.GetAsync(TestOwner.Id, Arg.Any<CancellationToken>()).Returns(Owner());
        _repository.ListOwnedIdsAsync(TestOwner.Id, 0, 50, Arg.Any<CancellationToken>()).Returns(new[] { _story.Id }, Array.Empty<Guid>());
        var service = new AccountDeletionService(_artists, _repository, _media, _hasher);
        // Act
        AccountDeletion result = await service.DeleteAsync(TestOwner.Id, "long password", CancellationToken.None);
        // Assert
        Assert.Equal(AccountDeletion.Deleted, result);
        Received.InOrder(() =>
        {
            _repository.DeleteAsync(_story.Id, _story.ReviewVersion, Arg.Any<CancellationToken>());
            _media.DeleteAsync(_voice.Id, _voice.File, Arg.Any<CancellationToken>());
            _artists.DeleteAsync(TestOwner.Id, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task DeleteAccountAsync_WrongPasswordOrMissingArtist_DeletesNothing()
    {
        // Arrange
        _artists.GetAsync(TestOwner.Id, Arg.Any<CancellationToken>()).Returns(Owner());
        var service = new AccountDeletionService(_artists, _repository, _media, _hasher);
        // Act / Assert
        Assert.Equal(AccountDeletion.WrongPassword, await service.DeleteAsync(TestOwner.Id, "wrong password", CancellationToken.None));
        Assert.Equal(AccountDeletion.NotFound, await service.DeleteAsync(Guid.NewGuid(), "long password", CancellationToken.None));
        await _repository.DidNotReceiveWithAnyArgs().DeleteAsync(default, default, default);
        await _artists.DidNotReceiveWithAnyArgs().DeleteAsync(default, default);
    }

    [Fact]
    public async Task DeleteAccountAsync_StoryChangesDuringDelete_RetriesWithNewVersion()
    {
        // Arrange
        _artists.GetAsync(TestOwner.Id, Arg.Any<CancellationToken>()).Returns(Owner());
        _repository.ListOwnedIdsAsync(TestOwner.Id, 0, 50, Arg.Any<CancellationToken>()).Returns(new[] { _story.Id }, new[] { _story.Id }, Array.Empty<Guid>());
        _repository.DeleteAsync(_story.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(_ => throw new StoryConflictException(), _ => true);
        var service = new AccountDeletionService(_artists, _repository, _media, _hasher);
        // Act
        AccountDeletion result = await service.DeleteAsync(TestOwner.Id, "long password", CancellationToken.None);
        // Assert
        Assert.Equal(AccountDeletion.Deleted, result);
        await _repository.Received(2).DeleteAsync(_story.Id, Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _artists.Received(1).DeleteAsync(TestOwner.Id, Arg.Any<CancellationToken>());
    }
}
