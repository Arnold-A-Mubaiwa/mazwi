using Mazwi.Domain;

namespace Mazwi.Application;

public sealed record LanguagePlaysDto(string Language, int Plays, int PlaysLast7Days);

public sealed record StoryStatsDto(Guid StoryId, int Plays, int PlaysLast7Days, IReadOnlyList<LanguagePlaysDto> Languages)
{
    public static IReadOnlyList<StoryStatsDto> From(IEnumerable<StoryPlayTotal> totals) => totals
        .GroupBy(total => total.StoryId)
        .Select(story => new StoryStatsDto(story.Key, story.Sum(total => total.Plays), story.Sum(total => total.PlaysLast7Days),
            story.OrderByDescending(total => total.Plays).ThenBy(total => total.Language, StringComparer.Ordinal)
                .Select(total => new LanguagePlaysDto(total.Language, total.Plays, total.PlaysLast7Days)).ToArray()))
        .ToArray();
}
