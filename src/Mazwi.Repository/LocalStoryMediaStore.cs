using Mazwi.Domain;

namespace Mazwi.Repository;

public sealed class LocalStoryMediaStore(string root) : IStoryMediaStore
{
    public async Task SaveAsync(Guid id, StoryRecording recording, Stream content, CancellationToken cancellationToken)
    {
        string destination = FilePath(id, recording);
        Directory.CreateDirectory(root);
        string temporary = Path.Combine(root, $"{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
            {
                byte[] header = new byte[12];
                int headerBytes = await content.ReadAtLeastAsync(header, header.Length, false, cancellationToken);
                if (!MatchesHeader(recording.Extension, header.AsSpan(0, headerBytes)))
                {
                    throw new DomainException("The recording contents do not match its file type.");
                }

                await output.WriteAsync(header.AsMemory(0, headerBytes), cancellationToken);
                long total = headerBytes;
                byte[] buffer = new byte[81920];
                int count;
                while ((count = await content.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += count;
                    if (total > recording.Bytes || total > StoryRecording.MaximumBytes)
                    {
                        throw new DomainException("The recording exceeds its declared size or the 25 MB limit.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
                }

                if (total != recording.Bytes)
                {
                    throw new DomainException("The recording upload was incomplete.");
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, false);
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    public Task DeleteAsync(Guid id, StoryRecording recording, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(FilePath(id, recording));
        return Task.CompletedTask;
    }

    private string FilePath(Guid id, StoryRecording recording)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A media identity is required.", nameof(id));
        }

        return Path.Combine(root, $"{id:N}{recording.Extension}");
    }

    private static bool MatchesHeader(string extension, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 12)
        {
            return false;
        }

        return extension switch
        {
            ".wav" => bytes[..4].SequenceEqual("RIFF"u8) && bytes[8..12].SequenceEqual("WAVE"u8),
            ".mp4" or ".m4a" => bytes[4..8].SequenceEqual("ftyp"u8),
            ".mp3" => bytes[..3].SequenceEqual("ID3"u8) || (bytes[0] == 0xff && (bytes[1] & 0xe0) == 0xe0),
            ".webm" => bytes[..4].SequenceEqual(new byte[] { 0x1a, 0x45, 0xdf, 0xa3 }),
            _ => false,
        };
    }
}
