using System.Data;
using Dapper;
using Npgsql;

namespace Mazwi.Repository;

public sealed record ImportedStory(Guid Id, string Title, bool AlreadyPresent);

// Copies unowned stories by one artist name, with their drafts, approvals, timings and play counts, from another
// Mazwi database. Used once to move the owner's stories out of the old shared test database. Copied stories stay
// unowned so the owner claims them with --claim-stories. Media files are not copied: both databases use the same
// media folder. Nothing is written unless apply is true.
public sealed class StoryImporter(NpgsqlDataSource source, NpgsqlDataSource target)
{
    public async Task<IReadOnlyList<ImportedStory>> ImportUnownedAsync(string artistName, bool apply, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(artistName))
        {
            throw new ArgumentException("Name the artist whose stories to copy.", nameof(artistName));
        }

        await using NpgsqlConnection from = await source.OpenConnectionAsync(cancellationToken);
        StoryRow[] stories = (await from.QueryAsync<StoryRow>(new CommandDefinition("""
            SELECT id, title, artist_name AS ArtistName, community, country, story_type AS StoryType, original_language AS OriginalLanguage,
                recording_extension AS RecordingExtension, recording_bytes AS RecordingBytes, permission_recorded_at AS PermissionRecordedAt,
                transcript, review_version AS ReviewVersion
            FROM stories WHERE owner_id IS NULL AND artist_name = @ArtistName ORDER BY permission_recorded_at
            """, new { ArtistName = artistName.Trim() }, cancellationToken: cancellationToken))).ToArray();
        Guid[] ids = stories.Select(story => story.Id).ToArray();

        await using NpgsqlConnection to = await target.OpenConnectionAsync(cancellationToken);
        HashSet<Guid> present = (await to.QueryAsync<Guid>(new CommandDefinition(
            "SELECT id FROM stories WHERE id = ANY(@Ids)", new { Ids = ids }, cancellationToken: cancellationToken))).ToHashSet();
        ImportedStory[] result = stories.Select(story => new ImportedStory(story.Id, story.Title, present.Contains(story.Id))).ToArray();
        Guid[] missing = ids.Where(id => !present.Contains(id)).ToArray();
        if (!apply || missing.Length == 0)
        {
            return result;
        }

        var drafts = await from.QueryAsync<DraftRow>(new CommandDefinition("""
            SELECT story_id AS StoryId, language, text, notes, media_id AS MediaId, media_extension AS MediaExtension, media_bytes AS MediaBytes, media_voice AS MediaVoice
            FROM story_translation_drafts WHERE story_id = ANY(@Ids)
            """, new { Ids = missing }, cancellationToken: cancellationToken));
        var approvals = await from.QueryAsync<ApprovalRow>(new CommandDefinition("""
            SELECT story_id AS StoryId, language, approved_at AS ApprovedAt, speaker_checked AS SpeakerChecked FROM story_approvals WHERE story_id = ANY(@Ids)
            """, new { Ids = missing }, cancellationToken: cancellationToken));
        var timings = await from.QueryAsync<TimingRow>(new CommandDefinition("""
            SELECT story_id AS StoryId, language, starts_ms AS StartsMs, end_ms AS EndMs FROM story_subtitle_timings WHERE story_id = ANY(@Ids)
            """, new { Ids = missing }, cancellationToken: cancellationToken));
        var plays = await from.QueryAsync<PlayRow>(new CommandDefinition("""
            SELECT story_id AS StoryId, language, day::timestamp AS Day, plays FROM story_play_counts WHERE story_id = ANY(@Ids)
            """, new { Ids = missing }, cancellationToken: cancellationToken));

        await using NpgsqlTransaction transaction = await to.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        await to.ExecuteAsync(new CommandDefinition("""
            INSERT INTO stories (id, title, artist_name, community, country, story_type, original_language, recording_extension, recording_bytes,
                permission_recorded_at, transcript, review_version, owner_id)
            VALUES (@Id, @Title, @ArtistName, @Community, @Country, @StoryType, @OriginalLanguage, @RecordingExtension, @RecordingBytes,
                @PermissionRecordedAt, @Transcript, @ReviewVersion, NULL)
            ON CONFLICT (id) DO NOTHING
            """, stories.Where(story => missing.Contains(story.Id)), transaction, cancellationToken: cancellationToken));
        await to.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_translation_drafts (story_id, language, text, notes, media_id, media_extension, media_bytes, media_voice)
            VALUES (@StoryId, @Language, @Text, @Notes, @MediaId, @MediaExtension, @MediaBytes, @MediaVoice)
            """, drafts, transaction, cancellationToken: cancellationToken));
        await to.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_approvals (story_id, language, approved_at, speaker_checked) VALUES (@StoryId, @Language, @ApprovedAt, @SpeakerChecked)
            """, approvals, transaction, cancellationToken: cancellationToken));
        await to.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_subtitle_timings (story_id, language, starts_ms, end_ms) VALUES (@StoryId, @Language, @StartsMs, @EndMs)
            """, timings, transaction, cancellationToken: cancellationToken));
        await to.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_play_counts (story_id, language, day, plays) VALUES (@StoryId, @Language, @Day::date, @Plays)
            """, plays, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private sealed class StoryRow
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string Community { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string StoryType { get; set; } = string.Empty;
        public string OriginalLanguage { get; set; } = string.Empty;
        public string RecordingExtension { get; set; } = string.Empty;
        public long RecordingBytes { get; set; }
        public DateTimeOffset PermissionRecordedAt { get; set; }
        public string? Transcript { get; set; }
        public Guid ReviewVersion { get; set; }
    }

    private sealed class DraftRow
    {
        public Guid StoryId { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string[] Notes { get; set; } = [];
        public Guid? MediaId { get; set; }
        public string? MediaExtension { get; set; }
        public long? MediaBytes { get; set; }
        public string? MediaVoice { get; set; }
    }

    private sealed class ApprovalRow
    {
        public Guid StoryId { get; set; }
        public string Language { get; set; } = string.Empty;
        public DateTimeOffset ApprovedAt { get; set; }
        public bool SpeakerChecked { get; set; }
    }

    private sealed class TimingRow
    {
        public Guid StoryId { get; set; }
        public string Language { get; set; } = string.Empty;
        public int[] StartsMs { get; set; } = [];
        public int EndMs { get; set; }
    }

    private sealed class PlayRow
    {
        public Guid StoryId { get; set; }
        public string Language { get; set; } = string.Empty;
        public DateTime Day { get; set; }
        public int Plays { get; set; }
    }
}
