using Mazwi.Domain;
using NSubstitute;

namespace Mazwi.Application.Tests;

public sealed class DemoSeedServiceTests
{
    private sealed class FixedClock : TimeProvider
    {
        public static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly IArtistAccountService _accounts = Substitute.For<IArtistAccountService>();
    private readonly IArtistRepository _artists = Substitute.For<IArtistRepository>();
    private readonly IStoryService _stories = Substitute.For<IStoryService>();
    private readonly IStoryDraftService _drafts = Substitute.For<IStoryDraftService>();
    private readonly IStoryReviewService _reviews = Substitute.For<IStoryReviewService>();

    private static readonly DemoContent Content = new("demo password 1",
        [new DemoArtist("one@mazwi.test"), new DemoArtist("two@mazwi.test")],
        [
            new DemoStory(0, "isiXhosa reading", "Volunteer", "isiXhosa speakers", "ZA", "story", "xh", "recordings/a.wav", "Molo"),
            new DemoStory(1, "Setswana reading", "Volunteer", "Setswana speakers", "ZA", "story", "tn", "recordings/b.wav", "Dumela"),
        ]);

    private DemoSeedService Service() => new(_accounts, _artists, _stories, _drafts, _reviews, new FixedClock());

    [Fact]
    public async Task SeedAsync_EmptyDatabase_SignsUpConfirmsUploadsWritesAndApproves()
    {
        // Arrange
        var created = new Dictionary<string, Artist>();
        foreach (DemoArtist demo in Content.Artists)
        {
            Artist artist = Artist.Rehydrate(Guid.NewGuid(), new EmailAddress(demo.Email), new PasswordHash("hash"), FixedClock.Now);
            created[demo.Email] = artist;
            _accounts.SignUpAsync(demo.Email, Content.Password, Arg.Any<CancellationToken>()).Returns(new ArtistDto(artist.Id, demo.Email));
            _artists.GetAsync(artist.Id, Arg.Any<CancellationToken>()).Returns(artist);
        }

        _artists.FindByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>()).Returns((Artist?)null);
        var storyIds = new List<Guid>();
        _stories.UploadAsync(Arg.Any<UploadStoryCommand>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var command = call.Arg<UploadStoryCommand>();
            var id = Guid.NewGuid();
            storyIds.Add(id);
            return new StoryDto(id, command.Title, command.ArtistName, command.Community, command.Country, command.Type, command.Language, "unpublished", FixedClock.Now);
        });
        Guid version = Guid.NewGuid();
        _drafts.SaveTranscriptAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => new StoryDraftDto(call.Arg<Guid>(), "T", "A", "xh", "unpublished", call.Arg<string>(), [], version));
        var opened = new List<string>();
        // Act
        DemoSeedResult result = await Service().SeedAsync(Content, story => { opened.Add(story.Recording); return new MemoryStream([1, 2, 3]); }, CancellationToken.None);
        // Assert
        Assert.Equal(new DemoSeedResult(false, 2, 2), result);
        Assert.All(created.Values, artist => Assert.NotNull(artist.EmailVerifiedAt));
        await _artists.Received(2).SaveSecurityAsync(Arg.Any<Artist>(), null, Arg.Any<CancellationToken>());
        Assert.Equal(["recordings/a.wav", "recordings/b.wav"], opened);
        await _stories.Received(1).UploadAsync(Arg.Is<UploadStoryCommand>(command => command.Language == "xh" && command.Permission
            && command.FileName == "a.wav" && command.Bytes == 3 && command.OwnerId == created["one@mazwi.test"].Id), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _stories.Received(1).UploadAsync(Arg.Is<UploadStoryCommand>(command => command.Language == "tn" && command.OwnerId == created["two@mazwi.test"].Id),
            Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await _drafts.Received(1).SaveTranscriptAsync(storyIds[0], "Molo", Arg.Any<CancellationToken>());
        await _reviews.Received(1).ApproveAsync(storyIds[1], "tn", version, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_AlreadySeeded_DoesNothing()
    {
        // Arrange
        Artist existing = Artist.Rehydrate(Guid.NewGuid(), new EmailAddress("one@mazwi.test"), new PasswordHash("hash"), FixedClock.Now);
        _artists.FindByEmailAsync(new EmailAddress("one@mazwi.test"), Arg.Any<CancellationToken>()).Returns(existing);
        // Act
        DemoSeedResult result = await Service().SeedAsync(Content, _ => throw new InvalidOperationException("must not open"), CancellationToken.None);
        // Assert
        Assert.Equal(new DemoSeedResult(true, 0, 0), result);
        await _accounts.DidNotReceiveWithAnyArgs().SignUpAsync(default!, default!, default);
        await _stories.DidNotReceiveWithAnyArgs().UploadAsync(default!, default!, default);
    }

    [Fact]
    public async Task SeedAsync_InvalidContent_ThrowsBeforeCreatingAnything()
    {
        // Arrange
        DemoContent noArtists = Content with { Artists = [] };
        DemoContent badOwner = Content with { Stories = [Content.Stories[0] with { Artist = 5 }] };
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => Service().SeedAsync(noArtists, _ => Stream.Null, CancellationToken.None));
        await Assert.ThrowsAsync<DomainException>(() => Service().SeedAsync(badOwner, _ => Stream.Null, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => Service().SeedAsync(null!, _ => Stream.Null, CancellationToken.None));
        await _accounts.DidNotReceiveWithAnyArgs().SignUpAsync(default!, default!, default);
    }
}
