namespace Mazwi.Domain.Tests;

public sealed class StoryTests
{
    private static StoryDetails Details() => new("  Tsuro  ", "  Rudo ", " Shona ", "zw", "folktale", "sn");

    [Fact]
    public void Upload_ConsentedStory_TrimsDetailsAndStaysUnpublished()
    {
        // Act
        Story story = Story.Upload(Details(), new StoryRecording("../../original.MP4", 12), true);
        // Assert
        Assert.NotEqual(Guid.Empty, story.Id);
        Assert.Equal("Tsuro", story.Details.Title);
        Assert.Equal("Rudo", story.Details.ArtistName);
        Assert.Equal("Shona", story.Details.Community);
        Assert.Equal("ZW", story.Details.Country);
        Assert.Equal(".mp4", story.Recording.Extension);
        Assert.Equal("unpublished", story.Status);
        Assert.InRange(story.PermissionRecordedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Upload_NoPermission_RejectsStory()
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => Story.Upload(Details(), new StoryRecording("clip.wav", 12), false));
    }

    [Theory]
    [InlineData("", "Artist", "Community", "ZW", "story", "sn")]
    [InlineData("Title", " ", "Community", "ZW", "story", "sn")]
    [InlineData("Title", "Artist", "", "ZW", "story", "sn")]
    [InlineData("Title", "Artist", "Community", "Z", "story", "sn")]
    [InlineData("Title", "Artist", "Community", "12", "story", "sn")]
    [InlineData("Title", "Artist", "Community", "ZW", "other", "sn")]
    [InlineData("Title", "Artist", "Community", "ZW", "story", "xx")]
    [InlineData("Title\n", "Artist\tname", "Community", "ZW", "story", "sn")]
    public void Constructor_InvalidDetails_RejectsInput(string title, string artist, string community, string country, string type, string language)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new StoryDetails(title, artist, community, country, type, language));
    }

    [Theory]
    [InlineData("story", "sn")]
    [InlineData("poem", "xh")]
    [InlineData("folktale", "sw")]
    [InlineData("song", "en")]
    public void Constructor_SupportedTypeAndLanguage_AcceptsInput(string type, string language)
    {
        // Act
        var details = new StoryDetails(new string('t', 200), new string('a', 100), new string('c', 100), "ZA", type, language);
        // Assert
        Assert.Equal(type, details.Type);
        Assert.Equal(language, details.Language);
    }

    [Theory]
    [InlineData(201, 1, 1)]
    [InlineData(1, 101, 1)]
    [InlineData(1, 1, 101)]
    public void Constructor_OverlongDetails_RejectsInput(int title, int artist, int community)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new StoryDetails(new string('t', title), new string('a', artist), new string('c', community), "ZW", "story", "sn"));
    }

    [Theory]
    [InlineData("clip.exe", 10)]
    [InlineData("clip.mp3", 0)]
    [InlineData("clip.wav", -1)]
    [InlineData("clip.mp4", 25000001)]
    [InlineData("", 12)]
    public void Constructor_InvalidRecording_RejectsInput(string name, long bytes)
    {
        // Act / Assert
        Assert.Throws<DomainException>(() => new StoryRecording(name, bytes));
    }

    [Theory]
    [InlineData("clip.mp3", 1)]
    [InlineData("clip.wav", 25000000)]
    [InlineData("clip.m4a", 12)]
    [InlineData("clip.mp4", 12)]
    [InlineData("clip.webm", 12)]
    public void Constructor_SupportedRecording_AcceptsBoundarySizes(string name, long bytes)
    {
        // Act / Assert
        Assert.Equal(bytes, new StoryRecording(name, bytes).Bytes);
    }

    [Fact]
    public void Rehydrate_InvalidIdentityOrPermissionTimestamp_RejectsState()
    {
        // Arrange
        var recording = new StoryRecording("clip.mp3", 12);
        // Act / Assert
        Assert.Throws<DomainException>(() => Story.Rehydrate(Guid.Empty, Details(), recording, DateTimeOffset.UtcNow));
        Assert.Throws<DomainException>(() => Story.Rehydrate(Guid.NewGuid(), Details(), recording, default));
        Assert.Throws<ArgumentNullException>(() => Story.Upload(null!, recording, true));
        Assert.Throws<ArgumentNullException>(() => Story.Upload(Details(), null!, true));
    }
}
