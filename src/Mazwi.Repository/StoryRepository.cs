using System.Data;
using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository;

public sealed class StoryRepository(NpgsqlDataSource dataSource) : IStoryRepository
{
    public async Task AddAsync(Story story, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(story);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO stories (id, title, artist_name, community, country, story_type, original_language,
                recording_extension, recording_bytes, permission_recorded_at, review_version, owner_id)
            VALUES (@Id, @Title, @ArtistName, @Community, @Country, @Type, @Language,
                @Extension, @Bytes, @PermissionRecordedAt, @ReviewVersion, @OwnerId)
            """, new
        {
            story.Id,
            story.Details.Title,
            story.Details.ArtistName,
            story.Details.Community,
            story.Details.Country,
            story.Details.Type,
            story.Details.Language,
            story.Recording.Extension,
            story.Recording.Bytes,
            story.PermissionRecordedAt,
            story.ReviewVersion,
            story.OwnerId
        }, cancellationToken: cancellationToken));
    }

    public async Task<Story?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead, cancellationToken);
        StoryRow? row = await connection.QuerySingleOrDefaultAsync<StoryRow>(new CommandDefinition("""
            SELECT s.id, s.title, s.artist_name AS ArtistName, s.community, s.country, s.story_type AS Type,
                s.original_language AS Language, s.recording_extension AS Extension, s.recording_bytes AS Bytes,
                s.permission_recorded_at AS PermissionRecordedAt, s.transcript, s.review_version AS ReviewVersion, s.owner_id AS OwnerId, s.hidden_for_review AS HiddenForReview,
                EXISTS (SELECT 1 FROM story_approvals a WHERE a.story_id = s.id AND a.language = s.original_language) AS OriginalApproved
            FROM stories s WHERE s.id = @Id
            """, new { Id = id }, transaction, cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        IEnumerable<TranslationRow> drafts = await connection.QueryAsync<TranslationRow>(new CommandDefinition("""
            SELECT d.language, d.text, d.notes, d.media_id AS MediaId, d.media_extension AS MediaExtension, d.media_bytes AS MediaBytes, d.media_voice AS MediaVoice,
                EXISTS (SELECT 1 FROM story_approvals a WHERE a.story_id = d.story_id AND a.language = d.language) AS IsApproved,
                EXISTS (SELECT 1 FROM story_approvals a WHERE a.story_id = d.story_id AND a.language = d.language AND a.speaker_checked) AS SpeakerChecked
            FROM story_translation_drafts d WHERE d.story_id = @Id ORDER BY d.language
            """, new { Id = id }, transaction, cancellationToken: cancellationToken));
        IEnumerable<TimingRow> timings = await connection.QueryAsync<TimingRow>(new CommandDefinition(
            "SELECT language, starts_ms AS StartsMs, end_ms AS EndMs FROM story_subtitle_timings WHERE story_id = @Id",
            new { Id = id }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return Story.Rehydrate(row.Id, new StoryDetails(row.Title, row.ArtistName, row.Community, row.Country, row.Type, row.Language),
            new StoryRecording(string.Concat("recording", row.Extension), row.Bytes), row.PermissionRecordedAt, row.Transcript,
            drafts.Select(d => new TranslationDraft(d.Language, d.Text,
                d.MediaId is Guid mediaId ? new TranslatedRecording(mediaId, new StoryRecording(string.Concat("recording", d.MediaExtension), d.MediaBytes!.Value), d.MediaVoice) : null,
                d.IsApproved, new TranslationNotes(d.Notes), d.SpeakerChecked)), row.ReviewVersion, row.OriginalApproved, row.OwnerId,
            timings.Select(t => KeyValuePair.Create(t.Language, new SubtitleTiming(t.StartsMs, t.EndMs))).ToArray(), row.HiddenForReview);
    }

    public async Task SaveSubtitleTimingAsync(Story story, string language, Guid expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(story);
        SubtitleTiming timing = story.SubtitleTimings.TryGetValue(language, out SubtitleTiming? saved) ? saved
            : throw new ArgumentException("The story has no timing for this language.", nameof(language));
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (await LockAsync(connection, transaction, story.Id, cancellationToken) != expectedVersion)
        {
            throw new StoryConflictException();
        }

        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_subtitle_timings (story_id, language, starts_ms, end_ms) VALUES (@Id, @Language, @StartsMs, @EndMs)
            ON CONFLICT (story_id, language) DO UPDATE SET starts_ms = EXCLUDED.starts_ms, end_ms = EXCLUDED.end_ms
            """, new { story.Id, Language = language, StartsMs = timing.StartsMs.ToArray(), timing.EndMs }, transaction, cancellationToken: cancellationToken));
        // Timing the original revokes every approval, as a transcript edit does; timing a translation revokes only that language.
        await connection.ExecuteAsync(new CommandDefinition("""
            DELETE FROM story_approvals a USING stories s
            WHERE s.id = a.story_id AND a.story_id = @Id AND (@Language = s.original_language OR a.language = @Language)
            """, new { story.Id, Language = language }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("UPDATE stories SET review_version = @ReviewVersion WHERE id = @Id",
            new { story.Id, story.ReviewVersion }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    // Moderation does not change the artist's content, so the review version is left alone.
    public async Task SaveHiddenForReviewAsync(Story story, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(story);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("UPDATE stories SET hidden_for_review = @HiddenForReview WHERE id = @Id",
            new { story.Id, story.HiddenForReview }, cancellationToken: cancellationToken));
    }

    public async Task SaveUnpublishAsync(Story story, Guid expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(story);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (await LockAsync(connection, transaction, story.Id, cancellationToken) != expectedVersion)
        {
            throw new StoryConflictException();
        }

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM story_approvals WHERE story_id = @Id", new { story.Id }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("UPDATE stories SET review_version = @ReviewVersion WHERE id = @Id",
            new { story.Id, story.ReviewVersion }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
    }

    // Child rows (drafts, approvals, timings, play counts) go with the story through ON DELETE CASCADE.
    public async Task<bool> DeleteAsync(Guid id, Guid expectedVersion, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        Guid? version = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT review_version FROM stories WHERE id = @Id FOR UPDATE", new { Id = id }, transaction, cancellationToken: cancellationToken));
        if (version is null)
        {
            return false;
        }

        if (version != expectedVersion)
        {
            throw new StoryConflictException();
        }

        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM stories WHERE id = @Id", new { Id = id }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static Task DeleteTimingAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, string language, CancellationToken cancellationToken) =>
        connection.ExecuteAsync(new CommandDefinition("DELETE FROM story_subtitle_timings WHERE story_id = @Id AND language = @Language",
            new { Id = id, Language = language }, transaction, cancellationToken: cancellationToken));

    public Task SaveTranscriptAsync(Story story, CancellationToken cancellationToken) =>
        SaveTranscriptCoreAsync(story, null, cancellationToken);

    public Task SaveAiTranscriptAsync(Story story, Guid expectedVersion, CancellationToken cancellationToken) =>
        SaveTranscriptCoreAsync(story, expectedVersion, cancellationToken);

    private async Task SaveTranscriptCoreAsync(Story story, Guid? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(story);
        if (story.Transcript is null)
        {
            throw new ArgumentException("A saved transcript is required.", nameof(story));
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        Guid version = await LockAsync(connection, transaction, story.Id, cancellationToken);
        if (expectedVersion.HasValue && version != expectedVersion.Value)
        {
            throw new StoryConflictException();
        }

        string? previous = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT transcript FROM stories WHERE id = @Id", new { story.Id }, transaction, cancellationToken: cancellationToken));
        if (previous != story.Transcript.Value)
        {
            await connection.ExecuteAsync(new CommandDefinition("UPDATE stories SET transcript = @Text, review_version = gen_random_uuid() WHERE id = @Id",
                new { story.Id, Text = story.Transcript.Value }, transaction, cancellationToken: cancellationToken));
            await connection.ExecuteAsync(new CommandDefinition("DELETE FROM story_approvals WHERE story_id = @Id", new { story.Id }, transaction, cancellationToken: cancellationToken));
            await DeleteTimingAsync(connection, transaction, story.Id, story.Details.Language, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveTranslationAsync(Guid storyId, TranslationDraft draft, CancellationToken cancellationToken) =>
        SaveTranslationCoreAsync(storyId, draft, null, cancellationToken);

    public Task SaveAiTranslationAsync(Guid storyId, TranslationDraft draft, Guid expectedVersion, CancellationToken cancellationToken) =>
        SaveTranslationCoreAsync(storyId, draft, expectedVersion, cancellationToken);

    private async Task SaveTranslationCoreAsync(Guid storyId, TranslationDraft draft, Guid? expectedVersion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        Guid version = await LockAsync(connection, transaction, storyId, cancellationToken);
        if (expectedVersion.HasValue && version != expectedVersion.Value)
        {
            throw new StoryConflictException();
        }

        if (expectedVersion.HasValue)
        {
            bool approved = await connection.QuerySingleAsync<bool>(new CommandDefinition("""
                SELECT EXISTS (SELECT 1 FROM story_approvals a JOIN stories s ON s.id = a.story_id
                    WHERE s.id = @Id AND a.language = s.original_language AND s.transcript IS NOT NULL)
                """, new { Id = storyId }, transaction, cancellationToken: cancellationToken));
            if (!approved)
            {
                throw new StoryConflictException();
            }
        }

        TranslationRow? previous = await connection.QuerySingleOrDefaultAsync<TranslationRow>(new CommandDefinition(
            "SELECT text, notes FROM story_translation_drafts WHERE story_id = @Id AND language = @Language", new { Id = storyId, draft.Language }, transaction, cancellationToken: cancellationToken));
        if (expectedVersion.HasValue || previous?.Text != draft.Text.Value || !previous.Notes.SequenceEqual(draft.Notes.Items))
        {
            await connection.ExecuteAsync(new CommandDefinition("""
                INSERT INTO story_translation_drafts (story_id, language, text, notes) VALUES (@Id, @Language, @Text, @Notes)
                ON CONFLICT (story_id, language) DO UPDATE SET text = EXCLUDED.text, notes = EXCLUDED.notes
                """, new { Id = storyId, draft.Language, Text = draft.Text.Value, Notes = draft.Notes.Items.ToArray() }, transaction, cancellationToken: cancellationToken));
            if (previous?.Text != draft.Text.Value)
            {
                await DeleteTimingAsync(connection, transaction, storyId, draft.Language, cancellationToken);
            }

            await InvalidateAsync(connection, transaction, storyId, draft.Language, cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveRecordingAsync(Guid storyId, TranslationDraft draft, Guid expectedVersion, CancellationToken cancellationToken)
    {
        TranslatedRecording recording = draft.Recording ?? throw new ArgumentException("A translated recording is required.", nameof(draft));
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (await LockAsync(connection, transaction, storyId, cancellationToken) != expectedVersion)
        {
            throw new StoryConflictException();
        }

        int rows = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE story_translation_drafts SET media_id = @MediaId, media_extension = @Extension, media_bytes = @Bytes, media_voice = @Voice
            WHERE story_id = @Id AND language = @Language
            """, new { Id = storyId, draft.Language, MediaId = recording.Id, recording.File.Extension, recording.File.Bytes, recording.Voice }, transaction, cancellationToken: cancellationToken));
        if (rows != 1)
        {
            throw new DomainException("Save the translation before adding its recording.");
        }

        await DeleteTimingAsync(connection, transaction, storyId, draft.Language, cancellationToken);
        await InvalidateAsync(connection, transaction, storyId, draft.Language, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task SaveApprovalAsync(Guid storyId, string language, Guid expectedVersion, bool speakerChecked, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (await LockAsync(connection, transaction, storyId, cancellationToken) != expectedVersion)
        {
            throw new StoryConflictException();
        }

        int rows = await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_approvals (story_id, language, speaker_checked)
            SELECT s.id, @Language, @Language = s.original_language OR @SpeakerChecked FROM stories s WHERE s.id = @Id AND s.transcript IS NOT NULL
                AND (@Language = s.original_language OR
                    (EXISTS (SELECT 1 FROM story_translation_drafts d WHERE d.story_id = s.id AND d.language = @Language)
                    AND EXISTS (SELECT 1 FROM story_approvals a WHERE a.story_id = s.id AND a.language = s.original_language)))
            ON CONFLICT (story_id, language) DO UPDATE SET approved_at = now(), speaker_checked = EXCLUDED.speaker_checked
            """, new { Id = storyId, Language = language, SpeakerChecked = speakerChecked }, transaction, cancellationToken: cancellationToken));
        if (rows != 1)
        {
            throw new DomainException("Save and approve the original before approving translations.");
        }

        await transaction.CommitAsync(cancellationToken);
    }

    // Deletes only the specified identity, including its child rows; tests use their own generated IDs.
    public async Task<IReadOnlyList<Guid>> ListIdsAsync(bool approvedOnly, int offset, int limit, CancellationToken cancellationToken)
    {
        if (offset < 0 || limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<Guid> ids = await connection.QueryAsync<Guid>(new CommandDefinition("""
            SELECT s.id FROM stories s WHERE NOT @ApprovedOnly OR NOT s.hidden_for_review AND
                EXISTS (SELECT 1 FROM story_approvals a WHERE a.story_id = s.id AND a.language = s.original_language)
            ORDER BY s.permission_recorded_at DESC, s.id DESC LIMIT @Limit OFFSET @Offset
            """, new { ApprovedOnly = approvedOnly, Offset = offset, Limit = limit }, cancellationToken: cancellationToken));
        return ids.ToArray();
    }

    public async Task<IReadOnlyList<Guid>> ListOwnedIdsAsync(Guid ownerId, int offset, int limit, CancellationToken cancellationToken)
    {
        if (offset < 0 || limit is < 1 or > 50)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<Guid> ids = await connection.QueryAsync<Guid>(new CommandDefinition("""
            SELECT s.id FROM stories s WHERE s.owner_id = @OwnerId
            ORDER BY s.permission_recorded_at DESC, s.id DESC LIMIT @Limit OFFSET @Offset
            """, new { OwnerId = ownerId, Offset = offset, Limit = limit }, cancellationToken: cancellationToken));
        return ids.ToArray();
    }

    public async Task<int> ClaimUnownedAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE stories SET owner_id = @OwnerId WHERE owner_id IS NULL", new { OwnerId = ownerId }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM stories WHERE id = @Id", new { Id = id }, cancellationToken: cancellationToken));
    }

    private static async Task<Guid> LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, CancellationToken cancellationToken)
    {
        Guid? version = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT review_version FROM stories WHERE id = @Id FOR UPDATE", new { Id = id }, transaction, cancellationToken: cancellationToken));
        return version ?? throw new InvalidOperationException("The story could not be updated.");
    }

    private static async Task InvalidateAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, string language, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition("DELETE FROM story_approvals WHERE story_id = @Id AND language = @Language",
            new { Id = id, Language = language }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("UPDATE stories SET review_version = gen_random_uuid() WHERE id = @Id",
            new { Id = id }, transaction, cancellationToken: cancellationToken));
    }

    private sealed class TimingRow
    {
        public string Language { get; set; } = string.Empty;
        public int[] StartsMs { get; set; } = [];
        public int EndMs { get; set; }
    }

    private sealed class TranslationRow
    {
        public string Language { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public string[] Notes { get; set; } = [];
        public Guid? MediaId { get; set; }
        public string? MediaExtension { get; set; }
        public long? MediaBytes { get; set; }
        public string? MediaVoice { get; set; }
        public bool IsApproved { get; set; }
        public bool SpeakerChecked { get; set; }
    }

    private sealed class StoryRow
    {
        public Guid? OwnerId { get; set; }
        public bool HiddenForReview { get; set; }
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public string Community { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Language { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long Bytes { get; set; }
        public DateTimeOffset PermissionRecordedAt { get; set; }
        public string? Transcript { get; set; }
        public Guid ReviewVersion { get; set; }
        public bool OriginalApproved { get; set; }
    }
}
