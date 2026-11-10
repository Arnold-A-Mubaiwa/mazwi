using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Mazwi.Ai;
using Mazwi.Domain;

namespace Mazwi.WebApi.Tests;

public sealed class OpenAiSpeechGeneratorTests
{
    private const string Key = "private-test-secret";

    /// <summary>Scripted Realtime server: replies to each client event with the configured server events.</summary>
    private sealed class FakeRealtimeSocket : WebSocket
    {
        private readonly Queue<byte[]> _incoming = new();
        public List<JsonDocument> Sent { get; } = [];
        public Func<string, IEnumerable<object>> Reply { get; set; } = _ => [];
        public bool HangAfterSetup { get; set; }
        private WebSocketState _state = WebSocketState.Open;

        public FakeRealtimeSocket(IEnumerable<object>? greeting = null) => Enqueue(greeting ?? [new { type = "session.created" }]);

        public void Enqueue(IEnumerable<object> events)
        {
            foreach (object item in events)
            {
                _incoming.Enqueue(item as byte[] ?? JsonSerializer.SerializeToUtf8Bytes(item));
            }
        }

        public override Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType messageType, bool endOfMessage, CancellationToken cancellationToken)
        {
            var document = JsonDocument.Parse(buffer.ToArray());
            Sent.Add(document);
            Enqueue(Reply(document.RootElement.GetProperty("type").GetString()!));
            return Task.CompletedTask;
        }

        public override async Task<WebSocketReceiveResult> ReceiveAsync(ArraySegment<byte> buffer, CancellationToken cancellationToken)
        {
            if (_incoming.Count == 0)
            {
                if (HangAfterSetup)
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }

                return new WebSocketReceiveResult(0, WebSocketMessageType.Close, true);
            }

            byte[] message = _incoming.Dequeue();
            message.CopyTo(buffer.Array!, buffer.Offset);
            return new WebSocketReceiveResult(message.Length, WebSocketMessageType.Text, true);
        }

        public override WebSocketCloseStatus? CloseStatus => null;
        public override string? CloseStatusDescription => null;
        public override WebSocketState State => _state;
        public override string? SubProtocol => null;
        public override void Abort() => _state = WebSocketState.Aborted;
        public override Task CloseAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) { _state = WebSocketState.Closed; return Task.CompletedTask; }
        public override Task CloseOutputAsync(WebSocketCloseStatus closeStatus, string? statusDescription, CancellationToken cancellationToken) { _state = WebSocketState.CloseSent; return Task.CompletedTask; }
        public override void Dispose() => _state = WebSocketState.Closed;
    }

    private static object Audio(int bytes) => new { type = "response.output_audio.delta", delta = Convert.ToBase64String(new byte[bytes]) };
    private static object Done(string status = "completed") => new { type = "response.done", response = new { status } };

    private static FakeRealtimeSocket WorkingSocket(int bytesPerPart = 4) => new()
    {
        Reply = type => type switch
        {
            "session.update" => [new { type = "session.updated" }],
            "response.create" => [new { type = "response.output_audio_transcript.delta", delta = "ignored" }, Audio(bytesPerPart), Done()],
            _ => []
        }
    };

    private static (OpenAiSpeechGenerator Generator, List<(Uri Uri, string Key)> Connections) Generator(FakeRealtimeSocket socket, string? key = Key, TimeSpan? timeout = null)
    {
        var connections = new List<(Uri, string)>();
        var generator = new OpenAiSpeechGenerator(key, "gpt-realtime-2.1-mini", (uri, apiKey, _) =>
        {
            connections.Add((uri, apiKey));
            return Task.FromResult<WebSocket>(socket);
        }, timeout);
        return (generator, connections);
    }

    [Fact]
    public async Task GenerateAsync_TwoPartScript_UsesOneConnectionAndReturnsWav()
    {
        // Arrange
        FakeRealtimeSocket socket = WorkingSocket();
        var (generator, connections) = Generator(socket);
        var script = new SpeechScript(string.Concat(Enumerable.Repeat(new string('a', 399) + ". ", 2)));
        // Act
        GeneratedSpeech speech = await generator.GenerateAsync(script, "xh", "poem", CancellationToken.None);
        // Assert
        var connection = Assert.Single(connections);
        Assert.Equal("wss://api.openai.com/v1/realtime?model=gpt-realtime-2.1-mini", connection.Uri.ToString());
        Assert.Equal(Key, connection.Key);
        Assert.Equal(SpeechScript.WavHeaderBytes + 8, speech.Length);
        Assert.Equal(2, socket.Sent.Count(m => m.RootElement.GetProperty("type").GetString() == "response.create"));
        JsonElement session = socket.Sent[0].RootElement.GetProperty("session");
        Assert.Equal("marin", session.GetProperty("audio").GetProperty("output").GetProperty("voice").GetString());
        Assert.Equal(24000, session.GetProperty("audio").GetProperty("output").GetProperty("format").GetProperty("rate").GetInt32());
        Assert.Contains("isiXhosa", session.GetProperty("instructions").GetString());
        Assert.Contains("line break", session.GetProperty("instructions").GetString());
        string passage = socket.Sent[1].RootElement.GetProperty("item").GetProperty("content")[0].GetProperty("text").GetString()!;
        Assert.Equal(script.Parts[0], JsonDocument.Parse(passage).RootElement.GetProperty("passage").GetString());
    }

    [Theory]
    [InlineData("story", "grandmother")]
    [InlineData("folktale", "lively")]
    [InlineData("poem", "line break")]
    [InlineData("song", "Do not sing")]
    public void Instructions_StoryType_SetsMatchingStyle(string type, string expected)
    {
        // Act
        string instructions = OpenAiSpeechGenerator.Instructions("Shona", type);
        // Assert
        Assert.Contains(expected, instructions);
        Assert.Contains("verbatim in Shona", instructions);
    }

    [Fact]
    public void Wav_Pcm_WritesMono24KhzPcm16Header()
    {
        // Act
        byte[] wav = OpenAiSpeechGenerator.Wav([1, 2, 3, 4]);
        // Assert
        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVEfmt ", Encoding.ASCII.GetString(wav, 8, 8));
        Assert.Equal(1, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22)));
        Assert.Equal(24000, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)));
        Assert.Equal(16, BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34)));
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40)));
        Assert.Equal([1, 2, 3, 4], wav[44..]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Wav_EmptyOrOddPcm_Throws(int bytes)
    {
        // Act / Assert
        Assert.Throws<InvalidOperationException>(() => OpenAiSpeechGenerator.Wav(new byte[bytes]));
    }

    [Fact]
    public async Task GenerateAsync_MissingKey_FailsSafelyWithoutConnecting()
    {
        // Arrange
        var (generator, connections) = Generator(WorkingSocket(), key: null);
        // Act / Assert
        await Assert.ThrowsAsync<SpeechUnavailableException>(() => generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", CancellationToken.None));
        Assert.Empty(connections);
    }

    [Fact]
    public async Task GenerateAsync_AudioOverTwoMinutes_FailsSafely()
    {
        // Arrange
        var (generator, _) = Generator(WorkingSocket(SpeechScript.MaximumAudioBytes + 2));
        // Act / Assert
        await Assert.ThrowsAsync<SpeechUnavailableException>(() => generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_UpstreamError_FailsSafelyWithoutLeakingOrRetrying()
    {
        // Arrange
        FakeRealtimeSocket socket = WorkingSocket();
        socket.Reply = type => type == "session.update" ? [new { type = "error", error = new { message = "invalid key " + Key } }] : [];
        var (generator, connections) = Generator(socket);
        // Act
        SpeechUnavailableException error = await Assert.ThrowsAsync<SpeechUnavailableException>(() => generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", CancellationToken.None));
        // Assert
        Assert.DoesNotContain(Key, error.ToString());
        Assert.Null(error.InnerException);
        Assert.False(error.TimedOut);
        Assert.Single(connections);
    }

    [Fact]
    public async Task GenerateAsync_IncompleteResponse_FailsSafely()
    {
        // Arrange
        FakeRealtimeSocket socket = WorkingSocket();
        socket.Reply = type => type switch
        {
            "session.update" => [new { type = "session.updated" }],
            "response.create" => [Audio(4), Done("incomplete")],
            _ => []
        };
        var (generator, _) = Generator(socket);
        // Act / Assert
        await Assert.ThrowsAsync<SpeechUnavailableException>(() => generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_ConnectionClosedEarlyOrMalformed_FailsSafely()
    {
        // Arrange
        var closed = new FakeRealtimeSocket([]);
        var malformed = new FakeRealtimeSocket([Encoding.UTF8.GetBytes("not json")]);
        // Act / Assert
        await Assert.ThrowsAsync<SpeechUnavailableException>(() => Generator(closed).Generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", CancellationToken.None));
        await Assert.ThrowsAsync<SpeechUnavailableException>(() => Generator(malformed).Generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", CancellationToken.None));
    }

    [Fact]
    public async Task GenerateAsync_ConnectFailure_FailsSafely()
    {
        // Arrange
        var generator = new OpenAiSpeechGenerator(Key, connect: (_, _, _) => throw new WebSocketException("handshake 401 " + Key));
        // Act
        SpeechUnavailableException error = await Assert.ThrowsAsync<SpeechUnavailableException>(() => generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", CancellationToken.None));
        // Assert
        Assert.DoesNotContain(Key, error.ToString());
    }

    [Fact]
    public async Task GenerateAsync_Timeout_ThrowsTimedOut()
    {
        // Arrange
        FakeRealtimeSocket socket = WorkingSocket();
        socket.HangAfterSetup = true;
        socket.Reply = type => type == "session.update" ? [new { type = "session.updated" }] : [];
        var (generator, _) = Generator(socket, timeout: TimeSpan.FromMilliseconds(50));
        // Act
        SpeechUnavailableException error = await Assert.ThrowsAsync<SpeechUnavailableException>(() => generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", CancellationToken.None));
        // Assert
        Assert.True(error.TimedOut);
    }

    [Fact]
    public async Task GenerateAsync_CallerCancels_PropagatesCancellation()
    {
        // Arrange
        FakeRealtimeSocket socket = WorkingSocket();
        socket.HangAfterSetup = true;
        socket.Reply = type => type == "session.update" ? [new { type = "session.updated" }] : [];
        var (generator, _) = Generator(socket);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => generator.GenerateAsync(new SpeechScript("Molo"), "xh", "story", cancellation.Token));
    }
}
