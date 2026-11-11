namespace Mazwi.Application;

/// <summary>Demo artists and stories for judges and local tries (see demo/demo.json).</summary>
public sealed record DemoContent(string Password, IReadOnlyList<DemoArtist> Artists, IReadOnlyList<DemoStory> Stories);

public sealed record DemoArtist(string Email);

/// <param name="Artist">Index into <see cref="DemoContent.Artists"/>.</param>
/// <param name="Recording">Path of the recording, relative to the demo folder.</param>
public sealed record DemoStory(int Artist, string Title, string ArtistName, string Community, string Country, string Type,
    string Language, string Recording, string Words);

public sealed record DemoSeedResult(bool AlreadySeeded, int Artists, int Stories);
