namespace RomsConverter.Core.Conversion;

/// <param name="SourcePath">Archivo suelto (.cue/.iso) o comprimido.</param>
public sealed record ConversionJob(
    Guid Id,
    ConsoleKind Console,
    string SourcePath,
    string OutputDirectory);
