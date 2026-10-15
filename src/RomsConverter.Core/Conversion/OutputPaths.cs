namespace RomsConverter.Core.Conversion;

public static class OutputPaths
{
    public static string ExtensionFor(ConsoleKind console) =>
        console == ConsoleKind.PlayStation ? ".chd" : ".cso";

    /// <summary>
    /// Salida por defecto: misma carpeta y mismo nombre base que la entrada
    /// (comprimido, .cue o .iso) con la extensión nueva.
    /// </summary>
    public static string DefaultFor(ConsoleKind console, string sourcePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(sourcePath))!;
        return Path.Combine(directory, Path.GetFileNameWithoutExtension(sourcePath) + ExtensionFor(console));
    }
}
