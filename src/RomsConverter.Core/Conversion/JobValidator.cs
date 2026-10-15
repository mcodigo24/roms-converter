using System.Text.RegularExpressions;

namespace RomsConverter.Core.Conversion;

/// <summary>
/// Valida jobs sobre archivos sueltos (.cue/.iso). Los comprimidos solo pasan el
/// chequeo de extensión: su contenido se valida tras extraerlos.
/// </summary>
public sealed partial class JobValidator : IJobValidator
{
    private static readonly string[] ArchiveExtensions = [".zip", ".7z", ".rar"];

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

        if (ArchiveExtensions.Contains(extension))
            return Result(errors);

        var expected = job.Console == ConsoleKind.PlayStation ? ".cue" : ".iso";
        if (extension != expected)
        {
            errors.Add($"Archivo no válido para {ConsoleName(job.Console)}: se esperaba {expected} o un comprimido (.zip, .7z, .rar), pero es {extension}.");
            return Result(errors);
        }

        if (job.Console == ConsoleKind.PlayStation)
            ValidateCueReferences(source, errors);

        var outputPath = GetOutputPath(job);
        if (File.Exists(outputPath))
            errors.Add($"Ya existe el archivo de salida: {Path.GetFileName(outputPath)}");

        return Result(errors);
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

    private static string GetOutputPath(ConversionJob job)
    {
        var extension = job.Console == ConsoleKind.PlayStation ? ".chd" : ".cso";
        var name = Path.GetFileNameWithoutExtension(job.SourcePath);
        return Path.Combine(job.OutputDirectory, name + extension);
    }

    private static string ConsoleName(ConsoleKind console) =>
        console == ConsoleKind.PlayStation ? "PlayStation" : "PSP";

    private static ValidationResult Result(List<string> errors) => new(errors.Count == 0, errors);

    [GeneratedRegex("""^\s*FILE\s+(?:"([^"]+)"|(\S+))\s+\S+""", RegexOptions.IgnoreCase)]
    private static partial Regex FileLine();
}
