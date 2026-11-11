using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository;

public sealed class StoryVersionRequestRepository(NpgsqlDataSource dataSource) : IStoryVersionRequestRepository
{
    public async Task AddAsync(VersionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_version_requests (story_id, language, request_count) VALUES (@StoryId, @Language, 1)
            ON CONFLICT (story_id, language) DO UPDATE SET request_count = story_version_requests.request_count + 1, updated_at = now()
            """, new { request.StoryId, request.Language }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<VersionRequestTotal>> ListForOwnerAsync(Guid ownerId, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<TotalRow> rows = await connection.QueryAsync<TotalRow>(new CommandDefinition("""
            SELECT r.story_id AS StoryId, r.language, r.request_count AS Count
            FROM story_version_requests r JOIN stories s ON s.id = r.story_id
            WHERE s.owner_id = @OwnerId
                AND NOT EXISTS (SELECT 1 FROM story_approvals a WHERE a.story_id = r.story_id AND a.language = r.language)
            ORDER BY r.request_count DESC, r.story_id, r.language
            """, new { OwnerId = ownerId }, cancellationToken: cancellationToken));
        return rows.Select(row => new VersionRequestTotal(row.StoryId, row.Language, row.Count)).ToArray();
    }

    private sealed class TotalRow
    {
        public Guid StoryId { get; set; }
        public string Language { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
