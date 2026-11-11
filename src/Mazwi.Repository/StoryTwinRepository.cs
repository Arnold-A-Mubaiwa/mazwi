using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository;

public sealed class StoryTwinRepository(NpgsqlDataSource dataSource) : IStoryTwinRepository
{
    public async Task<IReadOnlyList<StoryToFingerprint>> ListStoriesToFingerprintAsync(DateTimeOffset retryFailedBefore, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<WorkRow> rows = await connection.QueryAsync<WorkRow>(new CommandDefinition("""
            SELECT t.id AS StoryId, t.original_language AS Language, t.story_type AS StoryType, t.transcript AS Text
            FROM twinnable_stories t LEFT JOIN story_fingerprints f ON f.story_id = t.id
            WHERE f.story_id IS NULL
                OR f.source_hash <> encode(sha256(convert_to(t.transcript, 'UTF8')), 'hex')
                OR (f.gist IS NULL AND f.failed_at < @RetryFailedBefore)
            ORDER BY COALESCE(f.gist IS NULL AND f.failed_at IS NOT NULL, false), t.permission_recorded_at DESC, t.id
            LIMIT @Limit
            """, new { RetryFailedBefore = retryFailedBefore, Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(row => new StoryToFingerprint(row.StoryId, row.Language, row.StoryType, row.Text)).ToArray();
    }

    public async Task SaveFingerprintAsync(Guid storyId, string sourceHash, StoryFingerprint fingerprint, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fingerprint);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_fingerprints (story_id, source_hash, gist, vector, failed_at, updated_at)
            VALUES (@StoryId, @SourceHash, @Gist, @Vector, NULL, now())
            ON CONFLICT (story_id) DO UPDATE SET source_hash = excluded.source_hash, gist = excluded.gist, vector = excluded.vector,
                failed_at = NULL, updated_at = now()
            """, new { StoryId = storyId, SourceHash = sourceHash, fingerprint.Gist, Vector = fingerprint.Vector.ToArray() },
            cancellationToken: cancellationToken));
    }

    public async Task SaveFingerprintFailureAsync(Guid storyId, string sourceHash, DateTimeOffset failedAt, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_fingerprints (story_id, source_hash, gist, vector, failed_at, updated_at)
            VALUES (@StoryId, @SourceHash, NULL, NULL, @FailedAt, now())
            ON CONFLICT (story_id) DO UPDATE SET source_hash = excluded.source_hash, gist = NULL, vector = NULL,
                failed_at = excluded.failed_at, updated_at = now()
            """, new { StoryId = storyId, SourceHash = sourceHash, FailedAt = failedAt }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TwinCandidate>> ListCandidatesAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<CandidateRow> rows = await connection.QueryAsync<CandidateRow>(new CommandDefinition("""
            SELECT t.id AS StoryId, t.owner_id AS OwnerId, t.original_language AS Language, f.gist, f.vector
            FROM twinnable_stories t JOIN story_fingerprints f ON f.story_id = t.id
            WHERE f.gist IS NOT NULL AND f.source_hash = encode(sha256(convert_to(t.transcript, 'UTF8')), 'hex')
            ORDER BY t.id
            """, cancellationToken: cancellationToken));
        return rows.Select(row => new TwinCandidate(row.StoryId, row.OwnerId, row.Language, new StoryFingerprint(row.Gist, row.Vector))).ToArray();
    }

    public async Task AddSuggestionsAsync(IEnumerable<StoryTwin> twins, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(twins);
        StoryTwin[] pairs = twins.ToArray();
        if (pairs.Length == 0)
        {
            return;
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_twins (story_a, story_b, similarity, status) VALUES (@StoryA, @StoryB, @Similarity, @Status)
            ON CONFLICT (story_a, story_b) DO NOTHING
            """, pairs.Select(twin => new { twin.StoryA, twin.StoryB, twin.Similarity, Status = twin.Status.ToString() }),
            cancellationToken: cancellationToken));
    }

    public async Task<StoryTwin?> GetVisibleAsync(Guid story, Guid other, CancellationToken cancellationToken)
    {
        if (story == other)
        {
            return null;
        }

        (Guid a, Guid b) = StoryTwin.Pair(story, other);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        TwinRow? row = await connection.QuerySingleOrDefaultAsync<TwinRow>(new CommandDefinition("""
            SELECT p.story_a AS StoryA, p.story_b AS StoryB, p.similarity, p.status
            FROM story_twins p
            JOIN twinnable_stories ta ON ta.id = p.story_a
            JOIN twinnable_stories tb ON tb.id = p.story_b
            WHERE p.story_a = @A AND p.story_b = @B AND p.status <> 'Rejected'
            """, new { A = a, B = b }, cancellationToken: cancellationToken));
        return row is null ? null : StoryTwin.Rehydrate(row.StoryA, row.StoryB, row.Similarity, Enum.Parse<TwinStatus>(row.Status));
    }

    public async Task SaveStatusAsync(StoryTwin twin, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(twin);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE story_twins SET status = @Status, updated_at = now() WHERE story_a = @StoryA AND story_b = @StoryB
            """, new { twin.StoryA, twin.StoryB, Status = twin.Status.ToString() }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<TwinView>> ListForOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<ViewRow> rows = await connection.QueryAsync<ViewRow>(new CommandDefinition("""
            SELECT mine.id AS StoryId, mine.title AS StoryTitle, twin.id AS TwinStoryId, twin.title AS TwinTitle,
                twin.artist_name AS TwinArtistName, twin.original_language AS TwinLanguage, twin.community AS TwinCommunity, p.status
            FROM story_twins p
            JOIN twinnable_stories mine ON mine.id IN (p.story_a, p.story_b)
            JOIN twinnable_stories twin ON twin.id IN (p.story_a, p.story_b) AND twin.id <> mine.id
            WHERE mine.owner_id = @OwnerId AND twin.owner_id <> @OwnerId AND p.status <> 'Rejected'
            ORDER BY p.status DESC, p.similarity DESC, mine.title, twin.id
            LIMIT 200
            """, new { OwnerId = ownerId }, cancellationToken: cancellationToken));
        return rows.Select(row => new TwinView(row.StoryId, row.StoryTitle, row.TwinStoryId, row.TwinTitle, row.TwinArtistName,
            row.TwinLanguage, row.TwinCommunity, Enum.Parse<TwinStatus>(row.Status))).ToArray();
    }

    public async Task<IReadOnlyList<Guid>> ListConfirmedTwinIdsAsync(Guid storyId, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<Guid> ids = await connection.QueryAsync<Guid>(new CommandDefinition("""
            SELECT twin.id
            FROM story_twins p
            JOIN twinnable_stories mine ON mine.id = @StoryId AND mine.id IN (p.story_a, p.story_b)
            JOIN twinnable_stories twin ON twin.id IN (p.story_a, p.story_b) AND twin.id <> mine.id
            WHERE p.status = 'Confirmed'
            ORDER BY p.similarity DESC, twin.id
            LIMIT 20
            """, new { StoryId = storyId }, cancellationToken: cancellationToken));
        return ids.ToArray();
    }

    private sealed class WorkRow
    {
        public Guid StoryId { get; set; }
        public string Language { get; set; } = string.Empty;
        public string StoryType { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }

    private sealed class CandidateRow
    {
        public Guid StoryId { get; set; }
        public Guid OwnerId { get; set; }
        public string Language { get; set; } = string.Empty;
        public string Gist { get; set; } = string.Empty;
        public float[] Vector { get; set; } = [];
    }

    private sealed class TwinRow
    {
        public Guid StoryA { get; set; }
        public Guid StoryB { get; set; }
        public double Similarity { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    private sealed class ViewRow
    {
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = string.Empty;
        public Guid TwinStoryId { get; set; }
        public string TwinTitle { get; set; } = string.Empty;
        public string TwinArtistName { get; set; } = string.Empty;
        public string TwinLanguage { get; set; } = string.Empty;
        public string TwinCommunity { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
}
