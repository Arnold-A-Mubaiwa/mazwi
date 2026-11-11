using Mazwi.Domain;

namespace Mazwi.Application;

/// <summary>Seeds demo content through the same use cases artists use: sign up, upload, write the words, approve.</summary>
public sealed class DemoSeedService(IArtistAccountService accounts, IArtistRepository artists, IStoryService stories,
    IStoryDraftService drafts, IStoryReviewService reviews, TimeProvider clock) : IDemoSeedService
{
    public async Task<DemoSeedResult> SeedAsync(DemoContent content, Func<DemoStory, Stream> openRecording, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(openRecording);
        if (content.Artists.Count == 0)
        {
            throw new DomainException("The demo needs at least one artist.");
        }

        if (content.Stories.Any(story => story.Artist < 0 || story.Artist >= content.Artists.Count))
        {
            throw new DomainException("Every demo story must belong to one of the demo artists.");
        }

        if (await artists.FindByEmailAsync(new EmailAddress(content.Artists[0].Email), cancellationToken) is not null)
        {
            return new DemoSeedResult(true, 0, 0);
        }

        var owners = new List<Guid>();
        foreach (DemoArtist demo in content.Artists)
        {
            ArtistDto created = await accounts.SignUpAsync(demo.Email, content.Password, cancellationToken);
            Artist artist = (await artists.GetAsync(created.Id, cancellationToken))!;
            artist.VerifyEmail(clock.GetUtcNow());
            await artists.SaveSecurityAsync(artist, null, cancellationToken);
            owners.Add(created.Id);
        }

        foreach (DemoStory demo in content.Stories)
        {
            await using Stream recording = openRecording(demo);
            var command = new UploadStoryCommand(demo.Title, demo.ArtistName, demo.Community, demo.Country, demo.Type, demo.Language,
                true, Path.GetFileName(demo.Recording), recording.Length, owners[demo.Artist]);
            StoryDto story = await stories.UploadAsync(command, recording, cancellationToken);
            StoryDraftDto draft = (await drafts.SaveTranscriptAsync(story.Id, demo.Words, cancellationToken))!;
            await reviews.ApproveAsync(story.Id, demo.Language, draft.ReviewVersion, false, cancellationToken);
        }

        return new DemoSeedResult(false, owners.Count, content.Stories.Count);
    }
}
