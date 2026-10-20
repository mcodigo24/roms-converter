using RomsConverter.Core.Conversion;
using SharpCompress.Archives;

namespace RomsConverter.Core.Archives;

public sealed class ArchiveExtractor : IArchiveExtractor
{
    public async Task<string> ExtractAsync(
        string archivePath,
        string destinationDirectory,
        ConsoleKind console,
        CancellationToken cancellationToken = default)
    {
        var gameExtension = console == ConsoleKind.PlayStation ? ".cue" : ".iso";
        var consoleName = console == ConsoleKind.PlayStation ? "PlayStation" : "PSP";

        using var archive = ArchiveFactory.OpenArchive(archivePath, null);
        var entries = archive.Entries.Where(e => !e.IsDirectory && e.Key is not null).ToList();

        var games = entries
            .Where(e => string.Equals(Path.GetExtension(e.Key), gameExtension, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (games.Count == 0)
            throw new ArchiveContentException(
                $"El comprimido no contiene ningún archivo {gameExtension} para {consoleName}.");
        if (games.Count > 1)
            throw new ArchiveContentException(
                $"El comprimido contiene más de un juego ({games.Count} archivos {gameExtension}); solo se admite uno.");

        var root = Path.GetFullPath(destinationDirectory);
        Directory.CreateDirectory(root);
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;

        string? gamePath = null;
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var target = Path.GetFullPath(Path.Combine(root, entry.Key!));
            if (!target.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
                throw new ArchiveContentException($"Ruta no permitida dentro del comprimido: {entry.Key}");

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using (var output = File.Create(target))
            await using (var input = await entry.OpenEntryStreamAsync(cancellationToken))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            if (entry == games[0])
                gamePath = target;
        }

        return gamePath!;
    }
}
