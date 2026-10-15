using System.Diagnostics;
using System.Text;

namespace RomsConverter.Core.Processes;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessResult> RunAsync(
        string executablePath,
        IReadOnlyList<string> arguments,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        var lines = new List<string>();
        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var registration = cancellationToken.Register(() => TryKill(process));

        var pumps = Task.WhenAll(
            Pump(process.StandardOutput, lines, progress),
            Pump(process.StandardError, lines, progress));

        await process.WaitForExitAsync(CancellationToken.None);
        await pumps;

        cancellationToken.ThrowIfCancellationRequested();

        return new ProcessResult(process.ExitCode, lines);
    }

    // chdman y maxcso actualizan el progreso con '\r', así que se corta en '\r' y '\n'.
    private static async Task Pump(StreamReader reader, List<string> lines, IProgress<double>? progress)
    {
        var buffer = new char[1024];
        var current = new StringBuilder();

        void Flush()
        {
            if (current.Length == 0) return;
            var line = current.ToString();
            current.Clear();
            lock (lines) lines.Add(line);
            if (progress is not null && ProgressParser.TryParsePercent(line, out var percent))
                progress.Report(percent);
        }

        int read;
        while ((read = await reader.ReadAsync(buffer)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                if (buffer[i] is '\r' or '\n') Flush();
                else current.Append(buffer[i]);
            }
        }
        Flush();
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // El proceso ya terminó.
        }
    }
}
