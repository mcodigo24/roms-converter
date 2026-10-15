using RomsConverter.Core.Processes;

namespace RomsConverter.Core.Conversion;

/// <summary>Convierte .cue (+ .bin) a .chd con <c>chdman createcd</c>.</summary>
public sealed class ChdConverter(IProcessRunner runner, string chdmanPath) : IFileConverter
{
    public async Task ConvertAsync(
        string sourcePath,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // chdman no sobrescribe; además no hay que borrar un archivo que no creamos nosotros.
        if (File.Exists(destinationPath))
            throw new ConversionFailedException($"Ya existe el archivo de salida: {Path.GetFileName(destinationPath)}");

        try
        {
            var result = await runner.RunAsync(
                chdmanPath, ["createcd", "-i", sourcePath, "-o", destinationPath], progress, cancellationToken);

            if (result.ExitCode != 0)
            {
                var detail = result.OutputLines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
                throw new ConversionFailedException(
                    $"chdman terminó con código {result.ExitCode}." + (detail is null ? "" : $" {detail.Trim()}"));
            }
        }
        catch
        {
            DeletePartialOutput(destinationPath);
            throw;
        }
    }

    private static void DeletePartialOutput(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (IOException)
        {
            // Mejor esfuerzo: no ocultar el error original.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
