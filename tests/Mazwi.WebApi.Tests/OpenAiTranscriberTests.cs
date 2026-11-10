using System.Net;
using System.Text;
using Mazwi.Ai;
using Mazwi.Domain;

namespace Mazwi.WebApi.Tests;

public sealed class OpenAiTranscriberTests
{
    private const string Key = "private-test-secret";
    private static readonly byte[] M4aHeader = [0, 0, 0, 32, .. Encoding.ASCII.GetBytes("ftypM4A "), 1, 2, 3, 4];

    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body) };

    private static Task<StoryText> Transcribe(Handler handler, byte[]? audio = null, string fileName = "clip.wav", string? key = Key, TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var transcriber = new OpenAiTranscriber(new HttpClient(handler), key, "configured-model", timeout);
        audio ??= [1, 2, 3, 4];
        return transcriber.TranscribeAsync(new MemoryStream(audio), new StoryRecording(fileName, audio.Length), cancellationToken);
    }

    [Fact]
    public async Task TranscribeAsync_ValidResponse_SendsOnlyFileAndModelOnce()
    {
        // Arrange
        using var handler = new Handler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.openai.com/v1/audio/transcriptions", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            var form = Assert.IsType<MultipartFormDataContent>(request.Content);
            Assert.Equal(["file", "model"], form.Select(part => part.Headers.ContentDisposition!.Name!.Trim('"')));
            HttpContent file = form.First();
            Assert.Equal("recording.wav", file.Headers.ContentDisposition!.FileName!.Trim('"'));
            Assert.Equal("audio/wav", file.Headers.ContentType!.MediaType);
            Assert.Equal([1, 2, 3, 4], await file.ReadAsByteArrayAsync(token));
            Assert.Equal("configured-model", await form.Last().ReadAsStringAsync(token));
            return Json("{\"text\":\"  Kare kare\\nzvako  \",\"languages\":[{\"code\":\"sn\"}]}");
        });
        // Act
        StoryText transcript = await Transcribe(handler);
        // Assert
        Assert.Equal("Kare kare\nzvako", transcript.Value);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public void UploadMetadata_AudioOnlyMp4Header_UsesM4aAudioType()
    {
        // Act
        var (fileName, contentType) = OpenAiTranscriber.UploadMetadata(M4aHeader, ".mp4");
        // Assert
        Assert.Equal("recording.m4a", fileName);
        Assert.Equal("audio/mp4", contentType);
    }

    [Theory]
    [InlineData(".mp4", "recording.mp4", "video/mp4")]
    [InlineData(".mp3", "recording.mp3", "audio/mpeg")]
    [InlineData(".webm", "recording.webm", "audio/webm")]
    [InlineData(".m4a", "recording.m4a", "audio/mp4")]
    [InlineData(".wav", "recording.wav", "audio/wav")]
    public void UploadMetadata_OtherFiles_UseExtensionType(string extension, string expectedName, string expectedType)
    {
        // Act
        var (fileName, contentType) = OpenAiTranscriber.UploadMetadata([0, 0, 0, 0], extension);
        // Assert
        Assert.Equal(expectedName, fileName);
        Assert.Equal(expectedType, contentType);
    }

    [Fact]
    public async Task TranscribeAsync_M4aSavedAsMp4_UploadsAsM4a()
    {
        // Arrange
        string? uploadedName = null;
        using var handler = new Handler((request, _) =>
        {
            uploadedName = ((MultipartFormDataContent)request.Content!).First().Headers.ContentDisposition!.FileName!.Trim('"');
            return Task.FromResult(Json("{\"text\":\"Mhoro\"}"));
        });
        // Act
        await Transcribe(handler, M4aHeader, "clip.mp4");
        // Assert
        Assert.Equal("recording.m4a", uploadedName);
    }

    [Fact]
    public async Task TranscribeAsync_MissingKey_FailsSafelyWithoutRequest()
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Json("{}")));
        // Act / Assert
        await Assert.ThrowsAsync<TranscriptionUnavailableException>(() => Transcribe(handler, key: " "));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("{\"text\":\"\"}")]
    [InlineData("{\"text\":\"   \"}")]
    [InlineData("{\"other\":\"x\"}")]
    [InlineData("not json")]
    public async Task TranscribeAsync_EmptyOrMalformedResponse_FailsSafely(string body)
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Json(body)));
        // Act / Assert
        await Assert.ThrowsAsync<TranscriptionUnavailableException>(() => Transcribe(handler));
    }

    [Fact]
    public async Task TranscribeAsync_TranscriptOverStoryLimit_FailsSafely()
    {
        // Arrange
        string body = "{\"text\":\"" + new string('a', StoryText.MaximumLength + 1) + "\"}";
        using var handler = new Handler((_, _) => Task.FromResult(Json(body)));
        // Act / Assert
        await Assert.ThrowsAsync<TranscriptionUnavailableException>(() => Transcribe(handler));
    }

    [Fact]
    public async Task TranscribeAsync_OversizedResponse_FailsSafely()
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Json(new string(' ', 600_000) + "{\"text\":\"x\"}")));
        // Act / Assert
        await Assert.ThrowsAsync<TranscriptionUnavailableException>(() => Transcribe(handler));
    }

    [Fact]
    public async Task TranscribeAsync_ErrorStatus_FailsSafelyWithoutLeakingOrRetrying()
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Json("{\"error\":{\"message\":\"bad key " + Key + "\"}}", HttpStatusCode.Unauthorized)));
        // Act
        TranscriptionUnavailableException error = await Assert.ThrowsAsync<TranscriptionUnavailableException>(() => Transcribe(handler));
        // Assert
        Assert.DoesNotContain(Key, error.ToString());
        Assert.Null(error.InnerException);
        Assert.False(error.TimedOut);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task TranscribeAsync_NetworkFailure_FailsSafely()
    {
        // Arrange
        using var handler = new Handler((_, _) => throw new HttpRequestException("socket error " + Key));
        // Act
        TranscriptionUnavailableException error = await Assert.ThrowsAsync<TranscriptionUnavailableException>(() => Transcribe(handler));
        // Assert
        Assert.DoesNotContain(Key, error.ToString());
    }

    [Fact]
    public async Task TranscribeAsync_Timeout_ThrowsTimedOut()
    {
        // Arrange
        using var handler = new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Json("{}"); });
        // Act
        TranscriptionUnavailableException error = await Assert.ThrowsAsync<TranscriptionUnavailableException>(() => Transcribe(handler, timeout: TimeSpan.FromMilliseconds(50)));
        // Assert
        Assert.True(error.TimedOut);
    }

    [Fact]
    public async Task TranscribeAsync_CallerCancels_PropagatesCancellation()
    {
        // Arrange
        using var handler = new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Json("{}"); });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Transcribe(handler, cancellationToken: cancellation.Token));
    }
}
