using Dapper;
using Mazwi.Domain;
using Npgsql;

namespace Mazwi.Repository;

public sealed class StoryPlayRepository(NpgsqlDataSource dataSource) : IStoryPlayRepository
{
    public async Task RecordAsync(StoryPlay play, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(play);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO story_play_counts (story_id, language, day, plays) VALUES (@StoryId, @Language, @Day::date, 1)
            ON CONFLICT (story_id, language, day) DO UPDATE SET plays = story_play_counts.plays + 1
            """, new { play.StoryId, play.Language, Day = AsDate(play.Day) }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<StoryPlayTotal>> ListForOwnerAsync(Guid ownerId, DateOnly recentSince, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IEnumerable<TotalRow> rows = await connection.QueryAsync<TotalRow>(new CommandDefinition("""
            SELECT p.story_id AS StoryId, p.language, SUM(p.plays)::integer AS Plays,
                COALESCE(SUM(p.plays) FILTER (WHERE p.day >= @RecentSince::date), 0)::integer AS PlaysLast7Days
            FROM story_play_counts p JOIN stories s ON s.id = p.story_id
            WHERE s.owner_id = @OwnerId
            GROUP BY p.story_id, p.language
            """, new { OwnerId = ownerId, RecentSince = AsDate(recentSince) }, cancellationToken: cancellationToken));
        return rows.Select(row => new StoryPlayTotal(row.StoryId, row.Language, row.Plays, row.PlaysLast7Days)).ToArray();
    }

    // Dapper cannot bind DateOnly; the SQL casts this midnight value back to a date.
    private static DateTime AsDate(DateOnly day) => day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

    private sealed class TotalRow
    {
        public Guid StoryId { get; set; }
        public string Language { get; set; } = string.Empty;
        public int Plays { get; set; }
        public int PlaysLast7Days { get; set; }
    }
}
