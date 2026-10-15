using System.IO.Compression;
using RomsConverter.Core.Archives;
using RomsConverter.Core.Conversion;

namespace RomsConverter.Core.Tests;

public sealed class ConversionQueueTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RomsConverterTests", Guid.NewGuid().ToString());
    private readonly string _input;
    private readonly string _output;
    private readonly string _temp;

    public ConversionQueueTests()
    {
        _input = Directory.CreateDirectory(Path.Combine(_root, "in")).FullName;
        _output = Path.Combine(_root, "out");
        _temp = Path.Combine(_root, "tmp");
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private sealed class Collector : IProgress<ConversionProgress>
    {
        public List<ConversionProgress> Events { get; } = [];
        public void Report(ConversionProgress value) => Events.Add(value);
        public IEnumerable<ConversionStatus> StatusesOf(Guid id) => Events.Where(e => e.JobId == id).Select(e => e.Status);
        public ConversionStatus LastOf(Guid id) => Events.Last(e => e.JobId == id).Status;
    }

    private sealed class FakeConverter : IFileConverter
    {
        private int _active;
        public int MaxConcurrent { get; private set; }
        public List<string> Sources { get; } = [];
        public Func<string, CancellationToken, Task>? Behavior { get; set; }

        public async Task ConvertAsync(string sourcePath, string destinationPath,
            IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var now = Interlocked.Increment(ref _active);
            MaxConcurrent = Math.Max(MaxConcurrent, now);
            try
            {
                Sources.Add(sourcePath);
                progress?.Report(50);
                if (Behavior is not null) await Behavior(destinationPath, cancellationToken);
                else await Task.Delay(20, cancellationToken);
                File.WriteAllText(destinationPath, "converted");
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private ConversionQueue CreateQueue(FakeConverter converter, Func<string, long>? freeSpace = null) =>
        new(new JobValidator(), new ArchiveExtractor(), _ => converter, _temp, freeSpace);

    private ConversionJob Job(ConsoleKind console, string source, string? output = null) =>
        new(Guid.NewGuid(), console, source,
            output ?? Path.Combine(_output, Path.GetFileNameWithoutExtension(source) + OutputPaths.ExtensionFor(console)));

    private string WriteFile(string name, string content = "x")
    {
        var path = Path.Combine(_input, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string CreateZip(string name, params (string Name, string Content)[] files)
    {
        var path = Path.Combine(_input, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryName, content) in files)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
            writer.Write(content);
        }
        return path;
    }

    private bool TempIsEmpty(Guid id) => !Directory.Exists(Path.Combine(_temp, id.ToString()));

    [Fact]
    public async Task Loose_iso_converts_and_reports_progress()
    {
        var job = Job(ConsoleKind.Psp, WriteFile("game.iso"));
        var progress = new Collector();

        await CreateQueue(new FakeConverter()).RunAsync([job], progress);

        Assert.Equal(
            [ConversionStatus.Validating, ConversionStatus.Converting, ConversionStatus.Converting, ConversionStatus.Completed],
            progress.StatusesOf(job.Id));
        Assert.Contains(progress.Events, e => e.Status == ConversionStatus.Converting && e.Percent == 50);
        Assert.True(File.Exists(Path.Combine(_output, "game.cso")));
    }

    [Fact]
    public async Task Zip_is_extracted_converted_and_temp_is_removed()
    {
        var zip = CreateZip("pack.zip", ("game.cue", "FILE \"game.bin\" BINARY\n"), ("game.bin", "data"));
        var job = Job(ConsoleKind.PlayStation, zip);
        var converter = new FakeConverter();
        var progress = new Collector();

        await CreateQueue(converter).RunAsync([job], progress);

        Assert.Equal(ConversionStatus.Completed, progress.LastOf(job.Id));
        Assert.Contains(ConversionStatus.Extracting, progress.StatusesOf(job.Id));
        Assert.StartsWith(Path.GetFullPath(_temp), converter.Sources.Single());
        Assert.True(File.Exists(Path.Combine(_output, "pack.chd")));
        Assert.True(TempIsEmpty(job.Id));
    }

    [Fact]
    public async Task Jobs_run_one_at_a_time_in_order()
    {
        var jobs = new[]
        {
            Job(ConsoleKind.Psp, WriteFile("a.iso")),
            Job(ConsoleKind.Psp, WriteFile("b.iso")),
            Job(ConsoleKind.Psp, WriteFile("c.iso")),
        };
        var converter = new FakeConverter();

        await CreateQueue(converter).RunAsync(jobs, new Collector());

        Assert.Equal(1, converter.MaxConcurrent);
        Assert.Equal(["a.iso", "b.iso", "c.iso"], converter.Sources.Select(Path.GetFileName));
    }

    [Fact]
    public async Task Invalid_job_fails_and_queue_continues()
    {
        var bad = Job(ConsoleKind.PlayStation, WriteFile("bad.cue", "FILE \"falta.bin\" BINARY\n"));
        WriteFile("ok.bin");
        var good = Job(ConsoleKind.PlayStation, WriteFile("ok.cue", "FILE \"ok.bin\" BINARY\n"));
        var progress = new Collector();

        await CreateQueue(new FakeConverter()).RunAsync([bad, good], progress);

        Assert.Equal(ConversionStatus.Failed, progress.LastOf(bad.Id));
        Assert.Contains("falta.bin", progress.Events.Last(e => e.JobId == bad.Id).Message);
        Assert.Equal(ConversionStatus.Completed, progress.LastOf(good.Id));
    }

    [Fact]
    public async Task Archive_with_two_games_fails_and_cleans_temp()
    {
        var zip = CreateZip("multi.zip", ("a.iso", "x"), ("b.iso", "x"));
        var job = Job(ConsoleKind.Psp, zip);
        var progress = new Collector();

        await CreateQueue(new FakeConverter()).RunAsync([job], progress);

        Assert.Equal(ConversionStatus.Failed, progress.LastOf(job.Id));
        Assert.True(TempIsEmpty(job.Id));
    }

    [Fact]
    public async Task Converter_failure_marks_failed_and_cleans_temp()
    {
        var zip = CreateZip("pack.zip", ("game.iso", "x"));
        var job = Job(ConsoleKind.Psp, zip);
        var converter = new FakeConverter
        {
            Behavior = (_, _) => throw new ConversionFailedException("maxcso terminó con código 1."),
        };
        var progress = new Collector();

        await CreateQueue(converter).RunAsync([job], progress);

        Assert.Equal(ConversionStatus.Failed, progress.LastOf(job.Id));
        Assert.Contains("maxcso", progress.Events.Last().Message);
        Assert.True(TempIsEmpty(job.Id));
    }

    [Fact]
    public async Task Cancel_stops_current_job_and_remaining_jobs_do_not_run()
    {
        var zip = CreateZip("pack.zip", ("first.iso", "x"));
        var first = Job(ConsoleKind.Psp, zip);
        var second = Job(ConsoleKind.Psp, WriteFile("second.iso"));
        using var cts = new CancellationTokenSource();
        var converter = new FakeConverter
        {
            Behavior = async (_, ct) =>
            {
                cts.Cancel();
                await Task.Delay(Timeout.Infinite, ct);
            },
        };
        var progress = new Collector();

        await CreateQueue(converter).RunAsync([first, second], progress, cts.Token);

        Assert.Equal(ConversionStatus.Cancelled, progress.LastOf(first.Id));
        Assert.Empty(progress.StatusesOf(second.Id));
        Assert.Single(converter.Sources);
        Assert.True(TempIsEmpty(first.Id));
    }

    [Fact]
    public async Task Insufficient_free_space_fails_before_extracting()
    {
        var zip = CreateZip("pack.zip", ("game.iso", "x"));
        var job = Job(ConsoleKind.Psp, zip);
        var converter = new FakeConverter();
        var progress = new Collector();

        await CreateQueue(converter, freeSpace: _ => 0).RunAsync([job], progress);

        Assert.Equal(ConversionStatus.Failed, progress.LastOf(job.Id));
        Assert.Empty(converter.Sources);
        Assert.True(TempIsEmpty(job.Id));
    }

    [Fact]
    public async Task Source_files_are_unchanged()
    {
        var iso = WriteFile("game.iso", "contenido original");
        var before = File.ReadAllBytes(iso);

        await CreateQueue(new FakeConverter()).RunAsync([Job(ConsoleKind.Psp, iso)], new Collector());

        Assert.Equal(before, File.ReadAllBytes(iso));
    }

    [Fact]
    public async Task Running_twice_at_the_same_time_throws()
    {
        var iso = WriteFile("game.iso");
        var gate = new TaskCompletionSource();
        var converter = new FakeConverter { Behavior = (_, _) => gate.Task };
        var queue = CreateQueue(converter);

        var first = queue.RunAsync([Job(ConsoleKind.Psp, iso)], new Collector());
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => queue.RunAsync([Job(ConsoleKind.Psp, iso)], new Collector()));

        gate.SetResult();
        await first;
    }

    [Fact]
    public async Task Custom_output_path_is_created_with_its_directory()
    {
        var custom = Path.Combine(_root, "elegida", "sub", "mi salida.cso");
        var job = Job(ConsoleKind.Psp, WriteFile("game.iso"), custom);
        var progress = new Collector();

        await CreateQueue(new FakeConverter()).RunAsync([job], progress);

        Assert.Equal(ConversionStatus.Completed, progress.LastOf(job.Id));
        Assert.True(File.Exists(custom));
    }
}
