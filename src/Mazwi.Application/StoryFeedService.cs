using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class StoryFeedService(IStoryRepository repository, IStoryMediaStore mediaStore) : IStoryFeedService
{
    public async Task<StoryFeedPageDto> GetPageAsync(string language, int offset, int limit, CancellationToken cancellationToken)
    {
        if (!StoryDetails.SupportsLanguage(language))
        {
            throw new DomainException("Choose a supported viewing language.");
        }

        IReadOnlyList<Guid> ids = await repository.ListIdsAsync(true, offset, limit, cancellationToken);
        var items = new List<StoryViewDto>();
        foreach (Guid id in ids)
        {
            StoryViewDto? story = await GetAsync(id, language, cancellationToken);
            if (story is not null)
            {
                items.Add(story);
            }
        }
        return new(items, ids.Count == limit ? offset + limit : null);
    }

    public async Task<StoryViewDto?> GetAsync(Guid id, string language, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null)
        {
            return null;
        }

        var resolved = await ResolveAsync(story, language, false, cancellationToken);
        if (resolved is null)
        {
            return null;
        }

        await resolved.Value.Stream.DisposeAsync();
        ApprovedStoryVersion version = resolved.Value.Version;
        return new(story.Id, story.Details.Title, story.Details.ArtistName, story.Details.Community, story.Details.Country,
            story.Details.Type, story.Details.Language, version.TextLanguage, version.AudioLanguage, version.Text.Value,
            version.Recording.Extension, story.ReviewVersion,
            new[] { story.Details.Language }.Concat(story.Translations.Where(d => d.IsApproved).Select(d => d.Language)).ToArray(), story.Recording.Extension, version.Voice);
    }

    public async Task<RecordingPlaybackDto?> GetRecordingAsync(Guid id, string language, Guid reviewVersion, bool originalAudio, CancellationToken cancellationToken)
    {
        Story? story = await repository.GetAsync(id, cancellationToken);
        if (story is null || !story.OriginalApproved)
        {
            return null;
        }

        story.EnsureVersion(reviewVersion);
        var resolved = await ResolveAsync(story, language, originalAudio, cancellationToken);
        return resolved is null ? null : new(resolved.Value.Stream, RecordingPlaybackDto.ContentTypeFor(resolved.Value.Version.Recording.Extension));
    }

    private async Task<(ApprovedStoryVersion Version, Stream Stream)?> ResolveAsync(Story story, string language, bool originalAudio, CancellationToken cancellationToken)
    {
        ApprovedStoryVersion? version = story.SelectApprovedVersion(language, originalAudio);
        if (version is null)
        {
            return null;
        }

        Stream? stream = await mediaStore.OpenReadAsync(version.MediaId, version.Recording, cancellationToken);
        if (stream is null && version.MediaId != story.Id)
        {
            version = story.SelectApprovedVersion(language, true)!;
            stream = await mediaStore.OpenReadAsync(version.MediaId, version.Recording, cancellationToken);
        }
        return stream is null ? null : (version, stream);
    }
}
