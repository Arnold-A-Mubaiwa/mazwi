using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository;

public sealed class StoryReportRepository(NpgsqlDataSource dataSource) : IStoryReportRepository
{
    public async Task AddAsync(StoryReport report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_reports (id, story_id, reason, note, created_at) VALUES (@Id, @StoryId, @Reason, @Note, @CreatedAt)
            """, new { report.Id, report.StoryId, Reason = report.Reason.ToString(), report.Note, report.CreatedAt }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ReportedStory>> ListOpenAsync(int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<ReportRow> rows = await connection.QueryAsync<ReportRow>(new CommandDefinition("""
            SELECT s.id AS StoryId, s.title, s.artist_name AS ArtistName, s.hidden_for_review AS HiddenForReview, count(*)::integer AS OpenReports,
                array_agg(DISTINCT r.reason ORDER BY r.reason) AS Reasons,
                array_remove(array_agg(r.note ORDER BY r.created_at DESC), NULL) AS Notes,
                max(r.created_at) AS LatestReportAt
            FROM story_reports r JOIN stories s ON s.id = r.story_id
            WHERE r.resolved_at IS NULL
            GROUP BY s.id, s.title, s.artist_name, s.hidden_for_review
            ORDER BY max(r.created_at) DESC
            LIMIT @Limit
            """, new { Limit = limit }, cancellationToken: cancellationToken));
        return rows.Select(row => new ReportedStory(row.StoryId, row.Title, row.ArtistName, row.HiddenForReview, row.OpenReports,
            row.Reasons.Select(Enum.Parse<ReportReason>).ToArray(), row.Notes.Take(20).ToArray(), row.LatestReportAt)).ToArray();
    }

    public async Task ResolveAsync(Guid storyId, DateTimeOffset resolvedAt, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE story_reports SET resolved_at = @ResolvedAt WHERE story_id = @StoryId AND resolved_at IS NULL",
            new { StoryId = storyId, ResolvedAt = resolvedAt }, cancellationToken: cancellationToken));
    }

    private sealed class ReportRow
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ArtistName { get; set; } = string.Empty;
        public bool HiddenForReview { get; set; }
        public int OpenReports { get; set; }
        public string[] Reasons { get; set; } = [];
        public string[] Notes { get; set; } = [];
        public DateTimeOffset LatestReportAt { get; set; }
    }
}
