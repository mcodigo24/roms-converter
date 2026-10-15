using RomsConverter.Core.Processes;

namespace RomsConverter.Core.Conversion;

/// <summary>Convierte .cue (+ .bin) a .chd con <c>chdman createcd</c>.</summary>
public sealed class ChdConverter(IProcessRunner runner, string chdmanPath)
    : ExternalToolConverter(runner, chdmanPath, "chdman")
{
    protected override IReadOnlyList<string> BuildArguments(string sourcePath, string destinationPath) =>
        ["createcd", "-i", sourcePath, "-o", destinationPath];
}
