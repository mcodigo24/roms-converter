using System.Diagnostics;
using RomsConverter.Core.Processes;

namespace RomsConverter.Core.Tests;

// Usa cmd.exe como "exe falso": la app es solo Windows.
public sealed class ProcessRunnerTests
{
    private const string Cmd = "cmd.exe";
    private readonly ProcessRunner _runner = new();

    private sealed class ListProgress : IProgress<double>
    {
        public List<double> Values { get; } = [];
        public void Report(double value) { lock (Values) Values.Add(value); }
    }

    [Theory]
    [InlineData("Compressing, 45.3% complete... (ratio=50.0%)", 50.0)]
    [InlineData("Compressing, 12.5% complete...", 12.5)]
    [InlineData("progress 7%", 7)]
    [InlineData("Progreso 33,5 %", 33.5)]
    public void Parses_percent(string line, double expected)
    {
        Assert.True(ProgressParser.TryParsePercent(line, out var percent));
        Assert.Equal(expected, percent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("sin numeros")]
    [InlineData("200% raro")]
    public void Ignores_lines_without_valid_percent(string line)
    {
        Assert.False(ProgressParser.TryParsePercent(line, out _));
    }

    [Fact]
    public async Task Returns_exit_code_and_output()
    {
        var result = await _runner.RunAsync(Cmd, ["/c", "echo hola & exit 3"]);

        Assert.Equal(3, result.ExitCode);
        Assert.Contains(result.OutputLines, l => l.Contains("hola"));
    }

    [Fact]
    public async Task Reports_percent_from_stdout_and_stderr()
    {
        var progress = new ListProgress();

        var result = await _runner.RunAsync(
            Cmd, ["/c", "echo 10%& echo 55%>&2& echo 100%"], progress);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(10, progress.Values);
        Assert.Contains(55, progress.Values);
        Assert.Contains(100, progress.Values);
    }

    [Fact]
    public async Task Splits_progress_on_carriage_return()
    {
        var progress = new ListProgress();

        // PowerShell escribe "10%\r50%\r100%" en una sola línea física.
        await _runner.RunAsync(
            "powershell.exe",
            ["-NoProfile", "-Command", "[Console]::Out.Write(\"10%`r50%`r100%\")"],
            progress);

        Assert.Equal([10d, 50d, 100d], progress.Values);
    }

    [Fact]
    public async Task Cancellation_kills_process_and_throws()
    {
        using var cts = new CancellationTokenSource();
        var stopwatch = Stopwatch.StartNew();

        var task = _runner.RunAsync(Cmd, ["/c", "ping -n 30 127.0.0.1 >nul"], null, cts.Token);
        await Task.Delay(500);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(15), "El proceso no se detuvo al cancelar.");
    }

    [Fact]
    public async Task Already_cancelled_token_does_not_start_process()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _runner.RunAsync(Cmd, ["/c", "echo x"], null, cts.Token));
    }

    [Fact]
    public async Task Missing_executable_throws()
    {
        await Assert.ThrowsAnyAsync<Exception>(
            () => _runner.RunAsync(@"C:\no\existe\tool.exe", []));
    }
}
