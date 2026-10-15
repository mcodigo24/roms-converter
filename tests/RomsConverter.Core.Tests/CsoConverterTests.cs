using RomsConverter.Core.Conversion;
using RomsConverter.Core.Processes;

namespace RomsConverter.Core.Tests;

public sealed class CsoConverterTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RomsConverterTests", Guid.NewGuid().ToString());
    private readonly string _iso;
    private readonly string _cso;

    public CsoConverterTests()
    {
        Directory.CreateDirectory(_root);
        _iso = Path.Combine(_root, "game.iso");
        _cso = Path.Combine(_root, "game.cso");
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
    public async Task Calls_maxcso_with_input_and_output()
    {
        var runner = new FakeRunner((args, _, _) =>
        {
            File.WriteAllText(OutputArg(args), "cso");
            return Task.FromResult(new ProcessResult(0, []));
        });

        await new CsoConverter(runner, "maxcso.exe").ConvertAsync(_iso, _cso);

        Assert.Equal("maxcso.exe", runner.Executable);
        Assert.Equal([_iso, "-o", _cso], runner.Arguments);
        Assert.True(File.Exists(_cso));
    }

    [Fact]
    public async Task Failure_deletes_partial_output_and_throws_with_detail()
    {
        var runner = new FakeRunner((args, _, _) =>
        {
            File.WriteAllText(OutputArg(args), "parcial");
            return Task.FromResult(new ProcessResult(2, ["maxcso: no se pudo leer el iso"]));
        });

        var ex = await Assert.ThrowsAsync<ConversionFailedException>(
            () => new CsoConverter(runner, "maxcso.exe").ConvertAsync(_iso, _cso));

        Assert.Contains("maxcso", ex.Message);
        Assert.Contains("no se pudo leer", ex.Message);
        Assert.False(File.Exists(_cso));
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
            () => new CsoConverter(runner, "maxcso.exe").ConvertAsync(_iso, _cso, null, cts.Token));

        Assert.False(File.Exists(_cso));
    }

    [Fact]
    public async Task Existing_output_is_not_touched_nor_deleted()
    {
        File.WriteAllText(_cso, "del usuario");
        var runner = new FakeRunner((_, _, _) => throw new InvalidOperationException("no debería ejecutarse"));

        await Assert.ThrowsAsync<ConversionFailedException>(
            () => new CsoConverter(runner, "maxcso.exe").ConvertAsync(_iso, _cso));

        Assert.Equal("del usuario", File.ReadAllText(_cso));
        Assert.Null(runner.Executable);
    }
}
