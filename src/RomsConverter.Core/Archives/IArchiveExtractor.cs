using RomsConverter.Core.Conversion;

namespace RomsConverter.Core.Archives;

public interface IArchiveExtractor
{
    /// <summary>
    /// Extrae el comprimido a <paramref name="destinationDirectory"/> y devuelve la ruta del
    /// .cue (PlayStation) o .iso (PSP) extraído.
    /// </summary>
    /// <exception cref="ArchiveContentException">El contenido no tiene exactamente un juego de la consola.</exception>
    Task<string> ExtractAsync(
        string archivePath,
        string destinationDirectory,
        ConsoleKind console,
        CancellationToken cancellationToken = default);
}
