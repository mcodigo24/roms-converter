using RomsConverter.Core.Archives;

namespace RomsConverter.Core.Conversion;

/// <summary>
/// Procesa jobs en serie. Un fallo de un ítem no detiene la cola; cancelar sí
/// (el ítem actual queda Cancelled y los restantes no se procesan).
/// </summary>
public sealed class ConversionQueue(
    IJobValidator validator,
    IArchiveExtractor extractor,
    Func<ConsoleKind, IFileConverter> converterFor,
    string? tempRoot = null,
    Func<string, long>? freeSpaceProvider = null)
{
    private readonly string _tempRoot = tempRoot ?? Path.Combine(Path.GetTempPath(), "RomsConverter");
    private readonly Func<string, long> _freeSpace = freeSpaceProvider ?? DefaultFreeSpace;
    private int _running;

    public async Task RunAsync(
        IReadOnlyList<ConversionJob> jobs,
        IProgress<ConversionProgress> progress,
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _running, 1) == 1)
            throw new InvalidOperationException("La cola ya está en ejecución.");

        try
        {
            foreach (var job in jobs)
            {
                if (cancellationToken.IsCancellationRequested) return;

                var keepGoing = await RunJobAsync(job, progress, cancellationToken);
                if (!keepGoing) return;
            }
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    /// <returns>false si el job fue cancelado y la cola debe detenerse.</returns>
    private async Task<bool> RunJobAsync(
        ConversionJob job, IProgress<ConversionProgress> progress, CancellationToken ct)
    {
        void Report(ConversionStatus status, double? percent = null, string? message = null) =>
            progress.Report(new ConversionProgress(job.Id, status, percent, message));

        var tempDirectory = Path.Combine(_tempRoot, job.Id.ToString());

        try
        {
            Report(ConversionStatus.Validating);
            var validation = validator.Validate(job);
            if (!validation.IsValid)
            {
                Report(ConversionStatus.Failed, null, string.Join(Environment.NewLine, validation.Errors));
                return true;
            }

            var gameFile = job.SourcePath;
            if (ArchiveFileTypes.IsArchive(job.SourcePath))
            {
                Report(ConversionStatus.Extracting);
                EnsureFreeSpace(job.SourcePath);
                gameFile = await extractor.ExtractAsync(job.SourcePath, tempDirectory, job.Console, ct);

                var extracted = validator.Validate(job with { SourcePath = gameFile });
                if (!extracted.IsValid)
                {
                    Report(ConversionStatus.Failed, null, string.Join(Environment.NewLine, extracted.Errors));
                    return true;
                }
            }

            Directory.CreateDirectory(job.OutputDirectory);
            var destination = OutputPaths.For(job.Console, gameFile, job.OutputDirectory);

            Report(ConversionStatus.Converting, 0);
            await converterFor(job.Console).ConvertAsync(
                gameFile, destination,
                new SyncProgress(percent => Report(ConversionStatus.Converting, percent)),
                ct);

            Report(ConversionStatus.Completed, 100);
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            Report(ConversionStatus.Cancelled);
            return false;
        }
        catch (Exception ex)
        {
            Report(ConversionStatus.Failed, null, ex.Message);
            return true;
        }
        finally
        {
            DeleteTempDirectory(tempDirectory);
        }
    }

    // Cota inferior: el contenido extraído ocupa al menos lo que pesa el comprimido.
    private void EnsureFreeSpace(string archivePath)
    {
        long free;
        try
        {
            free = _freeSpace(_tempRoot);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            return; // No se pudo consultar: se confía en el error de E/S real si falta espacio.
        }

        var required = new FileInfo(archivePath).Length;
        if (free < required)
            throw new ConversionFailedException(
                $"Espacio libre insuficiente en la carpeta temporal: se necesitan al menos {required / (1024 * 1024)} MB.");
    }

    private static long DefaultFreeSpace(string path)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(path))
                   ?? throw new ArgumentException("Ruta sin raíz.", nameof(path));
        return new DriveInfo(root).AvailableFreeSpace;
    }

    private static void DeleteTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
            // Mejor esfuerzo.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    // Progress<T> postea al SynchronizationContext y puede reordenar; acá se necesita orden estricto.
    private sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
