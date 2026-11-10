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
        var command = new CommandDefinition(
            """
            INSERT INTO stories (id, title, artist_name, community, country, story_type, original_language,
                recording_extension, recording_bytes, permission_recorded_at, status)
            VALUES (@Id, @Title, @ArtistName, @Community, @Country, @Type, @Language,
                @Extension, @Bytes, @PermissionRecordedAt, @Status)
            """,
            new
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
                story.Status,
            }, cancellationToken: cancellationToken);
        await connection.ExecuteAsync(command);
    }

    public async Task<Story?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var command = new CommandDefinition(
            """
            SELECT id, title, artist_name AS ArtistName, community, country, story_type AS Type,
                original_language AS Language, recording_extension AS Extension, recording_bytes AS Bytes,
                permission_recorded_at AS PermissionRecordedAt
            FROM stories WHERE id = @Id
            """, new { Id = id }, cancellationToken: cancellationToken);
        StoryRow? row = await connection.QuerySingleOrDefaultAsync<StoryRow>(command);
        return row is null ? null : Story.Rehydrate(row.Id,
            new StoryDetails(row.Title, row.ArtistName, row.Community, row.Country, row.Type, row.Language),
            new StoryRecording(string.Concat("recording", row.Extension), row.Bytes), row.PermissionRecordedAt);
    }

    private sealed class StoryRow
    {
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
    }
}
