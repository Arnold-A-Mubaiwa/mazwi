using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository.Tests;

public sealed class StoryImporterTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // A second dedicated test database plays the new app database.
    private static async Task<NpgsqlDataSource> TargetAsync()
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("MAZWI_TEST_CONNECTION_STRING"));
        settings.Database = "mazwi_import_tests";
        new DatabaseMigrator(settings.ConnectionString).Migrate(CancellationToken.None);
        NpgsqlDataSource target = NpgsqlDataSource.Create(settings.ConnectionString);
        await using NpgsqlConnection connection = await target.OpenConnectionAsync();
        return target;
    }

    private async Task<Story> SourceStoryAsync(string artist)
    {
        var repository = new StoryRepository(fixture.DataSource);
        Story story = Story.Upload(new StoryDetails("Import fixture", artist, "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        await repository.AddAsync(story, CancellationToken.None);
        story.SetTranscript("Kare kare\nPane imwe nguva");
        await repository.SaveTranscriptAsync(story, CancellationToken.None);
        await repository.SaveTranslationAsync(story.Id, story.SetTranslation("xh", "Kudala\nKwakukho", new TranslationNotes(["A note"])), CancellationToken.None);
        story = (await repository.GetAsync(story.Id, CancellationToken.None))!;
        Guid version = story.ReviewVersion;
        story.SetSubtitleTiming("sn", new SubtitleTiming([0, 900], 2000), version);
        await repository.SaveSubtitleTimingAsync(story, "sn", version, CancellationToken.None);
        await repository.SaveApprovalAsync(story.Id, "sn", story.ReviewVersion, false, CancellationToken.None);
        await new StoryPlayRepository(fixture.DataSource).RecordAsync(new StoryPlay(story.Id, "sn", new DateOnly(2026, 11, 1)), CancellationToken.None);
        await using NpgsqlConnection connection = await fixture.DataSource.OpenConnectionAsync();
        // Stories saved before accounts have no owner; only those are imported.
        await connection.ExecuteAsync("UPDATE stories SET owner_id = NULL WHERE id = @Id", new { story.Id });
        return story;
    }

    [Fact]
    public async Task ImportUnownedAsync_DryRunThenApply_CopiesStoryWithEverythingAndStaysUnowned()
    {
        // Arrange
        string artist = $"Import artist {Guid.NewGuid():N}";
        await using NpgsqlDataSource target = await TargetAsync();
        Story story = await SourceStoryAsync(artist);
        var importer = new StoryImporter(fixture.DataSource, target);
        try
        {
            // Act
            IReadOnlyList<ImportedStory> preview = await importer.ImportUnownedAsync(artist, apply: false, CancellationToken.None);
            Story? beforeApply = await new StoryRepository(target).GetAsync(story.Id, CancellationToken.None);
            IReadOnlyList<ImportedStory> copied = await importer.ImportUnownedAsync($"  {artist} ", apply: true, CancellationToken.None);
            IReadOnlyList<ImportedStory> again = await importer.ImportUnownedAsync(artist, apply: true, CancellationToken.None);
            Story imported = (await new StoryRepository(target).GetAsync(story.Id, CancellationToken.None))!;
            // Assert
            Assert.False(Assert.Single(preview).AlreadyPresent);
            Assert.Null(beforeApply);
            Assert.False(Assert.Single(copied).AlreadyPresent);
            Assert.True(Assert.Single(again).AlreadyPresent);
            Assert.Null(imported.OwnerId);
            Assert.True(imported.OriginalApproved);
            Assert.Equal("Kare kare\nPane imwe nguva", imported.Transcript!.Value);
            Assert.Equal(["A note"], imported.Translations.Single().Notes.Items);
            Assert.Equal(new SubtitleTiming([0, 900], 2000), imported.SubtitleTimings["sn"]);
            await using NpgsqlConnection connection = await target.OpenConnectionAsync();
            Assert.Equal(1, await connection.ExecuteScalarAsync<int>("SELECT plays FROM story_play_counts WHERE story_id = @Id", new { story.Id }));
        }
        finally
        {
            await new StoryRepository(fixture.DataSource).DeleteAsync(story.Id, CancellationToken.None);
            await new StoryRepository(target).DeleteAsync(story.Id, CancellationToken.None);
        }
    }

    [Fact]
    public async Task ImportUnownedAsync_OwnedOrOtherArtistsStories_AreNotCopied()
    {
        // Arrange
        string artist = $"Import artist {Guid.NewGuid():N}";
        await using NpgsqlDataSource target = await TargetAsync();
        var repository = new StoryRepository(fixture.DataSource);
        Story owned = Story.Upload(new StoryDetails("Owned fixture", artist, "Shona", "ZW", "story", "sn"), new StoryRecording("clip.wav", 19), true, TestOwner.Id);
        await repository.AddAsync(owned, CancellationToken.None);
        try
        {
            // Act
            IReadOnlyList<ImportedStory> found = await new StoryImporter(fixture.DataSource, target).ImportUnownedAsync(artist, apply: true, CancellationToken.None);
            // Assert
            Assert.Empty(found);
            Assert.Null(await new StoryRepository(target).GetAsync(owned.Id, CancellationToken.None));
        }
        finally { await repository.DeleteAsync(owned.Id, CancellationToken.None); }
    }

    [Fact]
    public async Task ImportUnownedAsync_BlankArtist_Throws()
    {
        // Arrange
        await using NpgsqlDataSource target = await TargetAsync();
        // Act / Assert
        await Assert.ThrowsAsync<ArgumentException>(() => new StoryImporter(fixture.DataSource, target).ImportUnownedAsync(" ", false, CancellationToken.None));
    }
}
