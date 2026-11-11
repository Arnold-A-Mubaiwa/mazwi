using System.Net;
using System.Text.Json;
using Mazwi.Ai;
using Mazwi.Domain;

namespace Mazwi.WebApi.Tests;

public sealed class OpenAiTranslationDrafterTests
{
    private sealed class Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return respond(request, cancellationToken);
        }
    }

    private static HttpResponseMessage Envelope(string text, string status = "completed") => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(new { status, output = new[] { new { type = "message", content = new[] { new { type = "output_text", text } } } } }))
    };

    [Fact]
    public async Task DraftAsync_ValidResponse_SendsOneStructuredRequestAndPreservesPoem()
    {
        // Arrange
        using var handler = new Handler(async (request, token) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            JsonElement root = body.RootElement;
            Assert.Equal("configured-model", root.GetProperty("model").GetString());
            Assert.Equal("low", root.GetProperty("reasoning").GetProperty("effort").GetString());
            Assert.Equal("json_schema", root.GetProperty("text").GetProperty("format").GetProperty("type").GetString());
            Assert.True(root.GetProperty("text").GetProperty("format").GetProperty("strict").GetBoolean());
            Assert.Equal(2, root.GetProperty("text").GetProperty("format").GetProperty("schema").GetProperty("required").GetArrayLength());
            string instructions = root.GetProperty("instructions").GetString()!;
            foreach (string phrase in new[] { "Shona to Xhosa", "rhythm", "humour", "clan", "praise", "proverbs by meaning", "line breaks for poems" })
            {
                Assert.Contains(phrase, instructions);
            }

            using JsonDocument input = JsonDocument.Parse(root.GetProperty("input").GetString()!);
            Assert.Equal("First\nSecond", input.RootElement.GetProperty("passage").GetString());
            return Envelope("{\"translation\":\"Molo\\nSekuru\",\"notes\":[\"Name retained\"]}");
        });
        using var client = new HttpClient(handler);
        // Act
        TranslationDraftResult result = await new OpenAiTranslationDrafter(client, "test-key", "configured-model").DraftAsync("First\nSecond", "sn", "xh", "poem", CancellationToken.None);
        // Assert
        Assert.Equal("Molo\nSekuru", result.Translation.Value);
        Assert.Equal("Name retained", Assert.Single(result.Notes.Items));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("yo", "ha", "Yoruba to Hausa")]
    [InlineData("am", "om", "Amharic to Oromo")]
    [InlineData("nd", "ny", "Northern Ndebele (Zimbabwe) to Chichewa (Nyanja)")]
    public async Task DraftAsync_NewCatalogueLanguages_NamesBothLanguagesInEnglish(string source, string target, string phrase)
    {
        // Arrange
        using var handler = new Handler(async (request, token) =>
        {
            using JsonDocument body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));
            Assert.Contains(phrase, body.RootElement.GetProperty("instructions").GetString());
            return Envelope("{\"translation\":\"Draft\",\"notes\":[]}");
        });
        using var client = new HttpClient(handler);
        // Act
        TranslationDraftResult result = await new OpenAiTranslationDrafter(client, "test-key", "configured-model").DraftAsync("Words", source, target, "story", CancellationToken.None);
        // Assert
        Assert.Equal("Draft", result.Translation.Value);
    }

    [Fact]
    public async Task DraftAsync_LanguageOutsideCatalogue_FailsSafelyWithoutCallingOpenAi()
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Envelope("{}")));
        using var client = new HttpClient(handler);
        // Act / Assert
        await Assert.ThrowsAsync<TranslationDraftUnavailableException>(() =>
            new OpenAiTranslationDrafter(client, "test-key", "configured-model").DraftAsync("Words", "sn", "de", "story", CancellationToken.None));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task DraftAsync_UpstreamError_NoRetryOrSensitiveException(int status)
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("secret-key private provider error") }));
        using var client = new HttpClient(handler);
        // Act
        Exception exception = await Assert.ThrowsAsync<TranslationDraftUnavailableException>(() => new OpenAiTranslationDrafter(client, "secret-key").DraftAsync("Source", "sn", "xh", "story", CancellationToken.None));
        // Assert
        Assert.Equal(1, handler.Calls);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("secret-key", exception.ToString()); Assert.DoesNotContain("provider error", exception.ToString());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"translation\":\"\",\"notes\":[]}")]
    [InlineData("{\"translation\":\"Molo\",\"notes\":null}")]
    [InlineData("{\"translation\":\"Molo\",\"notes\":[null]}")]
    [InlineData("{\"translation\":\"Molo\",\"notes\":[],\"approve\":true}")]
    public async Task DraftAsync_MalformedOutput_FailsSafely(string output)
    {
        // Arrange
        using var handler = new Handler((_, _) => Task.FromResult(Envelope(output)));
        using var client = new HttpClient(handler);
        // Act / Assert
        await Assert.ThrowsAsync<TranslationDraftUnavailableException>(() => new OpenAiTranslationDrafter(client, "key").DraftAsync("Source", "sn", "xh", "story", CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task DraftAsync_IncompleteRefusedAndOversize_FailsSafely()
    {
        // Arrange / Act / Assert
        foreach (HttpResponseMessage response in new[] { Envelope("{}", "incomplete"),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"completed\",\"output\":[{\"type\":\"message\",\"content\":[{\"type\":\"refusal\",\"refusal\":\"private\"}]}]}") },
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(new string('x', 256_001)) } })
        {
            using var handler = new Handler((_, _) => Task.FromResult(response));
            using var client = new HttpClient(handler);
            await Assert.ThrowsAsync<TranslationDraftUnavailableException>(() => new OpenAiTranslationDrafter(client, "key").DraftAsync("Source", "sn", "xh", "story", CancellationToken.None));
        }
    }

    [Fact]
    public async Task DraftAsync_TimeoutAndCancellation_DistinguishesFailureWithoutRetry()
    {
        // Arrange
        using var handler = new Handler(async (_, token) => { await Task.Delay(Timeout.Infinite, token); return Envelope("{}"); });
        using var client = new HttpClient(handler);
        var adapter = new OpenAiTranslationDrafter(client, "key", timeout: TimeSpan.FromMilliseconds(20));
        // Act / Assert
        Assert.True((await Assert.ThrowsAsync<TranslationDraftUnavailableException>(() => adapter.DraftAsync("Source", "sn", "xh", "story", CancellationToken.None))).TimedOut);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => adapter.DraftAsync("Source", "sn", "xh", "story", cancelled.Token));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task DraftAsync_TransportErrorOrMissingKey_DoesNotLeakSecret()
    {
        // Arrange
        using var handler = new Handler((_, _) => throw new HttpRequestException("secret-key provider detail"));
        using var client = new HttpClient(handler);
        // Act / Assert
        TranslationDraftUnavailableException failure = await Assert.ThrowsAsync<TranslationDraftUnavailableException>(() => new OpenAiTranslationDrafter(client, "secret-key").DraftAsync("Source", "sn", "xh", "story", CancellationToken.None));
        Assert.DoesNotContain("secret-key", failure.ToString()); Assert.Null(failure.InnerException);
        await Assert.ThrowsAsync<TranslationDraftUnavailableException>(() => new OpenAiTranslationDrafter(client, null).DraftAsync("Source", "sn", "xh", "story", CancellationToken.None));
        Assert.Equal(1, handler.Calls);
    }
}
