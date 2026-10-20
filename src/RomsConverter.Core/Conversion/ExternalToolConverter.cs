using RomsConverter.Core.Processes;

namespace RomsConverter.Core.Conversion;

/// <summary>
/// Base de los convertidores que delegan en una herramienta externa: no sobrescribe,
/// reenvía progreso y borra la salida parcial al fallar o cancelar.
/// </summary>
public abstract class ExternalToolConverter(IProcessRunner runner, string toolPath, string toolName) : IFileConverter
{
    protected abstract IReadOnlyList<string> BuildArguments(string sourcePath, string destinationPath);

    public async Task ConvertAsync(
        string sourcePath,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        // Las herramientas no deben sobrescribir; además no hay que borrar un archivo que no creamos nosotros.
        if (File.Exists(destinationPath))
            throw new ConversionFailedException($"Ya existe el archivo de salida: {Path.GetFileName(destinationPath)}");

        try
        {
            var result = await runner.RunAsync(
                toolPath, BuildArguments(sourcePath, destinationPath), progress, cancellationToken);

            if (result.ExitCode != 0)
            {
                var detail = result.OutputLines.LastOrDefault(l => !string.IsNullOrWhiteSpace(l));
                throw new ConversionFailedException(
                    $"{toolName} terminó con código {result.ExitCode}." + (detail is null ? "" : $" {detail.Trim()}"));
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
