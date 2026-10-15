namespace RomsConverter.Core.Conversion;

public static class OutputPaths
{
    /// <summary>Nombre base del .cue/.iso + extensión nueva (.chd/.cso) dentro de la carpeta de salida.</summary>
    public static string For(ConsoleKind console, string gameFilePath, string outputDirectory)
    {
        var extension = console == ConsoleKind.PlayStation ? ".chd" : ".cso";
        return Path.Combine(outputDirectory, Path.GetFileNameWithoutExtension(gameFilePath) + extension);
    }
}
