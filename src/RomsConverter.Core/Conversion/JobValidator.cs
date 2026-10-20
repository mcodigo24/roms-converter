using System.Text.RegularExpressions;
using RomsConverter.Core.Archives;

namespace RomsConverter.Core.Conversion;

/// <summary>
/// Valida jobs sobre archivos sueltos (.cue/.iso). Los comprimidos solo pasan el
/// chequeo de extensión: su contenido se valida tras extraerlos.
/// </summary>
public sealed partial class JobValidator : IJobValidator
{
    /// <summary>
    /// true si la extensión sirve de entrada para la consola: el archivo de juego
    /// (.cue en PlayStation, .iso en PSP) o un comprimido.
    /// </summary>
    public static bool IsAcceptedSource(ConsoleKind console, string path) =>
        ArchiveFileTypes.IsArchive(path) ||
        string.Equals(Path.GetExtension(path), console == ConsoleKind.PlayStation ? ".cue" : ".iso",
            StringComparison.OrdinalIgnoreCase);

    public ValidationResult Validate(ConversionJob job)
    {
        var errors = new List<string>();
        var source = job.SourcePath;
        var extension = Path.GetExtension(source).ToLowerInvariant();

        if (!File.Exists(source))
        {
            errors.Add($"No existe el archivo: {Path.GetFileName(source)}");
            return Result(errors);
        }

        if (!ArchiveFileTypes.IsArchive(source))
        {
            var expected = job.Console == ConsoleKind.PlayStation ? ".cue" : ".iso";
            if (extension != expected)
            {
                errors.Add($"Archivo no válido para {ConsoleName(job.Console)}: se esperaba {expected} o un comprimido (.zip, .7z, .rar), pero es {extension}.");
                return Result(errors);
            }

            if (job.Console == ConsoleKind.PlayStation)
                ValidateCueReferences(source, errors);
        }

        ValidateOutput(job, errors);

        return Result(errors);
    }

    private static void ValidateOutput(ConversionJob job, List<string> errors)
    {
        var expected = OutputPaths.ExtensionFor(job.Console);
        if (string.IsNullOrWhiteSpace(job.OutputPath) ||
            !string.Equals(Path.GetExtension(job.OutputPath), expected, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"La salida debe ser un archivo {expected}.");
            return;
        }

        if (File.Exists(job.OutputPath))
            errors.Add($"Ya existe el archivo de salida: {Path.GetFileName(job.OutputPath)}");
    }

    private static void ValidateCueReferences(string cuePath, List<string> errors)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(cuePath))!;
        var referenced = new List<string>();

        foreach (var line in File.ReadLines(cuePath))
        {
            var match = FileLine().Match(line);
            if (!match.Success) continue;
            referenced.Add(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
        }

        if (referenced.Count == 0)
        {
            errors.Add($"El .cue no referencia ningún archivo (FILE): {Path.GetFileName(cuePath)}");
            return;
        }

        foreach (var name in referenced)
        {
            if (!File.Exists(Path.Combine(directory, name)))
                errors.Add($"Falta el archivo referenciado por el .cue: {name}");
        }
    }

    private static string ConsoleName(ConsoleKind console) =>
        console == ConsoleKind.PlayStation ? "PlayStation" : "PSP";

    private static ValidationResult Result(List<string> errors) => new(errors.Count == 0, errors);

    [GeneratedRegex("""^\s*FILE\s+(?:"([^"]+)"|(\S+))\s+\S+""", RegexOptions.IgnoreCase)]
    private static partial Regex FileLine();
}
