using System.IO.Compression;
using RomsConverter.Core.Archives;
using RomsConverter.Core.Conversion;

namespace RomsConverter.Core.Tests;

public sealed class ArchiveExtractorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RomsConverterTests", Guid.NewGuid().ToString());
    private readonly ArchiveExtractor _extractor = new();

    public ArchiveExtractorTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string CreateZip(params (string Name, string Content)[] files)
    {
        var path = Path.Combine(_root, Guid.NewGuid() + ".zip");
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, content) in files)
        {
            var entry = zip.CreateEntry(name);
            using var writer = new StreamWriter(entry.Open());
            writer.Write(content);
        }
        return path;
    }

    private string Destination() => Path.Combine(_root, "out", Guid.NewGuid().ToString());

    [Fact]
    public async Task Extracts_cue_and_bin_for_playstation()
    {
        var zip = CreateZip(("game.cue", "FILE \"game.bin\" BINARY"), ("game.bin", "data"));
        var dest = Destination();

        var cue = await _extractor.ExtractAsync(zip, dest, ConsoleKind.PlayStation);

        Assert.Equal(Path.Combine(Path.GetFullPath(dest), "game.cue"), cue);
        Assert.True(File.Exists(cue));
        Assert.Equal("data", File.ReadAllText(Path.Combine(dest, "game.bin")));
    }

    [Fact]
    public async Task Extracts_iso_for_psp()
    {
        var zip = CreateZip(("Juego ñandú.iso", "iso"));

        var iso = await _extractor.ExtractAsync(zip, Destination(), ConsoleKind.Psp);

        Assert.EndsWith("Juego ñandú.iso", iso);
        Assert.True(File.Exists(iso));
    }

    [Fact]
    public async Task Keeps_subfolder_structure()
    {
        var zip = CreateZip(("sub/game.cue", "x"), ("sub/game.bin", "y"));
        var dest = Destination();

        var cue = await _extractor.ExtractAsync(zip, dest, ConsoleKind.PlayStation);

        Assert.Equal(Path.Combine(Path.GetFullPath(dest), "sub", "game.cue"), cue);
        Assert.True(File.Exists(Path.Combine(dest, "sub", "game.bin")));
    }

    [Fact]
    public async Task Rejects_archive_without_game()
    {
        var zip = CreateZip(("readme.txt", "hola"));

        await Assert.ThrowsAsync<ArchiveContentException>(
            () => _extractor.ExtractAsync(zip, Destination(), ConsoleKind.PlayStation));
    }

    [Fact]
    public async Task Rejects_archive_with_wrong_console_content()
    {
        var zip = CreateZip(("game.iso", "x"));

        await Assert.ThrowsAsync<ArchiveContentException>(
            () => _extractor.ExtractAsync(zip, Destination(), ConsoleKind.PlayStation));
    }

    [Fact]
    public async Task Rejects_archive_with_more_than_one_game()
    {
        var zip = CreateZip(("a.cue", "x"), ("a.bin", "x"), ("b.cue", "x"), ("b.bin", "x"));
        var dest = Destination();

        await Assert.ThrowsAsync<ArchiveContentException>(
            () => _extractor.ExtractAsync(zip, dest, ConsoleKind.PlayStation));
        Assert.False(Directory.Exists(dest) && Directory.EnumerateFileSystemEntries(dest).Any());
    }

    [Fact]
    public async Task Rejects_path_traversal_entries()
    {
        var zip = CreateZip(("../evil.iso", "x"));

        await Assert.ThrowsAsync<ArchiveContentException>(
            () => _extractor.ExtractAsync(zip, Destination(), ConsoleKind.Psp));
        Assert.False(File.Exists(Path.Combine(_root, "out", "evil.iso")));
    }

    [Fact]
    public async Task Honors_cancellation()
    {
        var zip = CreateZip(("game.iso", "x"));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _extractor.ExtractAsync(zip, Destination(), ConsoleKind.Psp, cts.Token));
    }
}
