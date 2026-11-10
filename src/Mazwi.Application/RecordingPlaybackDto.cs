namespace Mazwi.Application;

public sealed record RecordingPlaybackDto(Stream Content, string ContentType)
{
    public static string ContentTypeFor(string extension) => extension switch
    {
        ".mp3" => "audio/mpeg",
        ".wav" => "audio/wav",
        ".m4a" => "audio/mp4",
        ".mp4" => "video/mp4",
        ".webm" => "video/webm",
        _ => "application/octet-stream",
    };
}
