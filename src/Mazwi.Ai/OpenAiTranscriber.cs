using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Mazwi.Domain;

namespace Mazwi.Ai;

/// <summary>
/// Transcribes a recording with one OpenAI transcription request. It sends only the documented
/// <c>file</c> and <c>model</c> fields: language/prompt hints failed in tools/language-check live tests.
/// </summary>
public sealed class OpenAiTranscriber(HttpClient client, string? apiKey, string model = "gpt-transcribe",
    TimeSpan? timeout = null) : ITranscriber
{
    private const int MaximumResponseBytes = 512_000;

    public async Task<StoryText> TranscribeAsync(Stream content, StoryRecording recording, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(recording);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            throw new TranscriptionUnavailableException();
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout ?? TimeSpan.FromMinutes(3));
        try
        {
            byte[] audio = await ReadAllAsync(content, StoryRecording.MaximumBytes, deadline.Token);
            (string fileName, string contentType) = UploadMetadata(audio, recording.Extension);
            using var file = new ByteArrayContent(audio);
            file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            using var form = new MultipartFormDataContent { { file, "file", fileName }, { new StringContent(model), "model" } };
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/audio/transcriptions") { Content = form };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using HttpResponseMessage response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            if (!response.IsSuccessStatusCode)
            {
                throw new TranscriptionUnavailableException();
            }

            await using Stream body = await response.Content.ReadAsStreamAsync(deadline.Token);
            using JsonDocument result = JsonDocument.Parse(await ReadAllAsync(body, MaximumResponseBytes, deadline.Token));
            return new StoryText(result.RootElement.GetProperty("text").GetString()!);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TranscriptionUnavailableException(timedOut: true);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or DomainException or ArgumentException or IOException)
        {
            // Never retain upstream bodies, keys, raw exception messages or inner exceptions.
            throw new TranscriptionUnavailableException();
        }
    }

    /// <summary>Audio-only ISO-BMFF files (brand M4A) are sent as .m4a even when saved as .mp4, matching tools/language-check.</summary>
    public static (string FileName, string ContentType) UploadMetadata(byte[] audio, string extension)
    {
        bool isM4a = audio.Length >= 12 && Encoding.ASCII.GetString(audio, 4, 4) == "ftyp" && Encoding.ASCII.GetString(audio, 8, 4).StartsWith("M4A", StringComparison.Ordinal);
        if (isM4a)
        {
            return ("recording.m4a", "audio/mp4");
        }

        return extension switch
        {
            ".wav" => ("recording.wav", "audio/wav"),
            ".mp3" => ("recording.mp3", "audio/mpeg"),
            ".m4a" => ("recording.m4a", "audio/mp4"),
            ".mp4" => ("recording.mp4", "video/mp4"),
            ".webm" => ("recording.webm", "audio/webm"),
            _ => throw new ArgumentException("Unsupported recording extension.")
        };
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, long maximum, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81_920];
        int count;
        while ((count = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + count > maximum)
            {
                throw new InvalidOperationException();
            }

            await buffer.WriteAsync(chunk.AsMemory(0, count), cancellationToken);
        }

        return buffer.ToArray();
    }
}
