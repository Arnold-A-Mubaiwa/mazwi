namespace Mazwi.Application;

public sealed record UploadStoryCommand(string Title, string ArtistName, string Community, string Country,
    string Type, string Language, bool Permission, string FileName, long Bytes);
