using RomsConverter.Core.Conversion;
using RomsConverter.Core.Processes;

namespace RomsConverter.Core.Tests;

public sealed class ChdConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RomsConverterTests", Guid.NewGuid().ToString());
    private readonly string _cue;
    private readonly string _chd;

    public ChdConverterTests()
    {
        Directory.CreateDirectory(_root);
        _cue = Path.Combine(_root, "game.cue");
        _chd = Path.Combine(_root, "game.chd");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class FakeRunner(Func<IReadOnlyList<string>, IProgress<double>?, CancellationToken, Task<ProcessResult>> run)
        : IProcessRunner
    {
        public string? Executable { get; private set; }
        public IReadOnlyList<string>? Arguments { get; private set; }

        public Task<ProcessResult> RunAsync(
            string executablePath, IReadOnlyList<string> arguments,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            Executable = executablePath;
            Arguments = arguments;
            return run(arguments, progress, cancellationToken);
        }
    }

    private static string OutputArg(IReadOnlyList<string> args) => args[args.ToList().IndexOf("-o") + 1];

    [Fact]
    public async Task Calls_chdman_createcd_with_input_and_output()
    {
        var runner = new FakeRunner((args, _, _) =>
        {
            File.WriteAllText(OutputArg(args), "chd");
            return Task.FromResult(new ProcessResult(0, []));
        });

        await new ChdConverter(runner, "chdman.exe").ConvertAsync(_cue, _chd);

        Assert.Equal("chdman.exe", runner.Executable);
        Assert.Equal(["createcd", "-i", _cue, "-o", _chd], runner.Arguments);
        Assert.True(File.Exists(_chd));
    }

    [Fact]
    public async Task Forwards_progress()
    {
        var reported = new List<double>();
        var runner = new FakeRunner((_, progress, _) =>
        {
            progress?.Report(42);
            return Task.FromResult(new ProcessResult(0, []));
        });

        await new ChdConverter(runner, "chdman.exe")
            .ConvertAsync(_cue, _chd, new Progress<double>(reported.Add));
        await Task.Delay(50); // Progress<T> postea al contexto

        Assert.Contains(42, reported);
    }

    [Fact]
    public async Task Failure_deletes_partial_output_and_throws_with_detail()
    {
        var runner = new FakeRunner((args, _, _) =>
        {
            File.WriteAllText(OutputArg(args), "parcial");
            return Task.FromResult(new ProcessResult(1, ["Error: no se pudo leer el .bin"]));
        });

        var ex = await Assert.ThrowsAsync<ConversionFailedException>(
            () => new ChdConverter(runner, "chdman.exe").ConvertAsync(_cue, _chd));

        Assert.Contains("no se pudo leer", ex.Message);
        Assert.False(File.Exists(_chd));
    }

    [Fact]
    public async Task Cancellation_deletes_partial_output()
    {
        using var cts = new CancellationTokenSource();
        var runner = new FakeRunner(async (args, _, ct) =>
        {
            File.WriteAllText(OutputArg(args), "parcial");
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            return new ProcessResult(0, []);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new ChdConverter(runner, "chdman.exe").ConvertAsync(_cue, _chd, null, cts.Token));

        Assert.False(File.Exists(_chd));
    }

    [Fact]
    public async Task Existing_output_is_not_touched_nor_deleted()
    {
        File.WriteAllText(_chd, "del usuario");
        var runner = new FakeRunner((_, _, _) => throw new InvalidOperationException("no debería ejecutarse"));

        await Assert.ThrowsAsync<ConversionFailedException>(
            () => new ChdConverter(runner, "chdman.exe").ConvertAsync(_cue, _chd));

        Assert.Equal("del usuario", File.ReadAllText(_chd));
        Assert.Null(runner.Executable);
    }

    [Fact]
    public async Task Runner_exception_deletes_partial_output()
    {
        var runner = new FakeRunner((args, _, _) =>
        {
            File.WriteAllText(OutputArg(args), "parcial");
            throw new IOException("boom");
        });

        await Assert.ThrowsAsync<IOException>(
            () => new ChdConverter(runner, "chdman.exe").ConvertAsync(_cue, _chd));

        Assert.False(File.Exists(_chd));
    }
}
