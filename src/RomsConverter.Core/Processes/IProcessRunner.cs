namespace RomsConverter.Core.Processes;

public sealed record ProcessResult(int ExitCode, IReadOnlyList<string> OutputLines);

public interface IProcessRunner
{
    /// <summary>
    /// Ejecuta <paramref name="executablePath"/> y espera a que termine. Reporta porcentajes (0-100)
    /// parseados de stdout/stderr. Si se cancela, mata el proceso (y sus hijos) y lanza
    /// <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
