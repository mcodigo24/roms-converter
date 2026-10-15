using RomsConverter.Core.Conversion;

namespace RomsConverter.Core.Tests;

public sealed class JobValidatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "RomsConverterTests", Guid.NewGuid().ToString());
    private readonly string _input;
    private readonly string _output;
    private readonly JobValidator _validator = new();

    public JobValidatorTests()
    {
        _input = Directory.CreateDirectory(Path.Combine(_root, "in")).FullName;
        _output = Directory.CreateDirectory(Path.Combine(_root, "out")).FullName;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Write(string name, string content = "x")
    {
        var path = Path.Combine(_input, name);
        File.WriteAllText(path, content);
        return path;
    }

    private ConversionJob Job(ConsoleKind console, string source) =>
        new(Guid.NewGuid(), console, source, _output);

    [Fact]
    public void Cue_with_existing_bin_is_valid()
    {
        Write("game.bin");
        var cue = Write("game.cue", "FILE \"game.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");

        var result = _validator.Validate(Job(ConsoleKind.PlayStation, cue));

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Cue_with_multiple_bins_is_valid_when_all_exist()
    {
        Write("game (Track 1).bin");
        Write("game (Track 2).bin");
        var cue = Write("game.cue",
            "FILE \"game (Track 1).bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n" +
            "FILE \"game (Track 2).bin\" BINARY\n  TRACK 02 AUDIO\n    INDEX 01 00:00:00\n");

        Assert.True(_validator.Validate(Job(ConsoleKind.PlayStation, cue)).IsValid);
    }

    [Fact]
    public void Cue_with_missing_bin_reports_missing_name()
    {
        var cue = Write("game.cue", "FILE \"falta.bin\" BINARY\n");

        var result = _validator.Validate(Job(ConsoleKind.PlayStation, cue));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("falta.bin"));
    }

    [Fact]
    public void Cue_with_non_ascii_and_spaces_in_names_is_valid()
    {
        Write("Pokémon Édición ñandú.bin");
        var cue = Write("Pokémon Édición ñandú.cue", "FILE \"Pokémon Édición ñandú.bin\" BINARY\n");

        Assert.True(_validator.Validate(Job(ConsoleKind.PlayStation, cue)).IsValid);
    }

    [Fact]
    public void Cue_without_file_entries_is_invalid()
    {
        var cue = Write("game.cue", "REM nada\n");

        Assert.False(_validator.Validate(Job(ConsoleKind.PlayStation, cue)).IsValid);
    }

    [Fact]
    public void Existing_chd_output_is_invalid()
    {
        Write("game.bin");
        var cue = Write("game.cue", "FILE \"game.bin\" BINARY\n");
        File.WriteAllText(Path.Combine(_output, "game.chd"), "x");

        var result = _validator.Validate(Job(ConsoleKind.PlayStation, cue));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("game.chd"));
    }

    [Fact]
    public void Iso_for_psp_is_valid()
    {
        var iso = Write("game.iso");

        Assert.True(_validator.Validate(Job(ConsoleKind.Psp, iso)).IsValid);
    }

    [Fact]
    public void Existing_cso_output_is_invalid()
    {
        var iso = Write("game.iso");
        File.WriteAllText(Path.Combine(_output, "game.cso"), "x");

        var result = _validator.Validate(Job(ConsoleKind.Psp, iso));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("game.cso"));
    }

    [Fact]
    public void Iso_on_playstation_is_rejected()
    {
        var iso = Write("game.iso");

        Assert.False(_validator.Validate(Job(ConsoleKind.PlayStation, iso)).IsValid);
    }

    [Fact]
    public void Cue_on_psp_is_rejected()
    {
        var cue = Write("game.cue", "FILE \"game.bin\" BINARY\n");

        Assert.False(_validator.Validate(Job(ConsoleKind.Psp, cue)).IsValid);
    }

    [Fact]
    public void Missing_source_is_invalid()
    {
        var result = _validator.Validate(Job(ConsoleKind.Psp, Path.Combine(_input, "no.iso")));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Archive_passes_extension_check()
    {
        var zip = Write("game.zip");

        Assert.True(_validator.Validate(Job(ConsoleKind.Psp, zip)).IsValid);
    }
}
