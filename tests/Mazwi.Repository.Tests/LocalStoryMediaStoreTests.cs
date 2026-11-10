using Mazwi.Domain;

namespace Mazwi.Repository.Tests;

public sealed class LocalStoryMediaStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mazwi-media-tests", Guid.NewGuid().ToString("N"));
    public static byte[] Wav() => "RIFF0000WAVEdata0000"u8.ToArray();

    [Theory]
    [InlineData(".wav", "RIFF0000WAVEdata")]
    [InlineData(".mp3", "ID30000000000000")]
    [InlineData(".mp4", "0000ftypisom0000")]
    [InlineData(".m4a", "0000ftypM4A 0000")]
    public async Task SaveAsync_ValidHeader_UsesIdentityInsteadOfUserPath(string extension, string contents)
    {
        // Arrange
        var store = new LocalStoryMediaStore(_root);
        byte[] bytes = System.Text.Encoding.ASCII.GetBytes(contents);
        using var stream = new MemoryStream(bytes);
        Guid id = Guid.NewGuid();
        var recording = new StoryRecording("../../outside" + extension, bytes.Length);
        // Act
        await store.SaveAsync(id, recording, stream, CancellationToken.None);
        // Assert
        string path = Assert.Single(Directory.GetFiles(_root));
        Assert.Equal(id.ToString("N") + extension, Path.GetFileName(path));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        await store.DeleteAsync(id, recording, CancellationToken.None);
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task SaveAsync_DeclaredSizeDoesNotMatch_RemovesTemporaryFile(int size)
    {
        // Arrange
        var store = new LocalStoryMediaStore(_root);
        using var stream = new MemoryStream(Wav());
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => store.SaveAsync(Guid.NewGuid(), new StoryRecording("clip.wav", size), stream, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Theory]
    [InlineData("not really audio content")]
    [InlineData("RIFF")]
    public async Task SaveAsync_InvalidHeader_RejectsAndRemovesTemporaryFile(string contents)
    {
        // Arrange
        var store = new LocalStoryMediaStore(_root);
        using var stream = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(contents));
        // Act / Assert
        await Assert.ThrowsAsync<DomainException>(() => store.SaveAsync(Guid.NewGuid(), new StoryRecording("clip.wav", stream.Length), stream, CancellationToken.None));
        Assert.Empty(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task SaveAsync_ExistingRecording_DoesNotOverwrite()
    {
        // Arrange
        var store = new LocalStoryMediaStore(_root);
        Guid id = Guid.NewGuid();
        var recording = new StoryRecording("clip.wav", Wav().Length);
        using var original = new MemoryStream(Wav());
        await store.SaveAsync(id, recording, original, CancellationToken.None);
        using var duplicate = new MemoryStream(Wav());
        // Act / Assert
        await Assert.ThrowsAsync<IOException>(() => store.SaveAsync(id, recording, duplicate, CancellationToken.None));
        Assert.Single(Directory.GetFiles(_root));
    }

    [Fact]
    public async Task SaveAsync_Cancelled_RemovesTemporaryFile()
    {
        // Arrange
        var store = new LocalStoryMediaStore(_root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var stream = new MemoryStream(Wav());
        // Act / Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(Guid.NewGuid(), new StoryRecording("clip.wav", stream.Length), stream, cancellation.Token));
        Assert.Empty(Directory.GetFiles(_root));
    }

    public void Dispose()
    {
        // The generated per-test directory contains only files created by this fixture.
        if (Directory.Exists(_root))
        {
            foreach (string file in Directory.GetFiles(_root))
            {
                File.Delete(file);
            }

            Directory.Delete(_root);
        }
    }
}
