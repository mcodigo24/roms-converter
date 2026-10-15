using RomsConverter.Core.Processes;

namespace RomsConverter.Core.Conversion;

/// <summary>Convierte .iso a .cso con <c>maxcso</c>.</summary>
public sealed class CsoConverter(IProcessRunner runner, string maxcsoPath)
    : ExternalToolConverter(runner, maxcsoPath, "maxcso")
{
    protected override IReadOnlyList<string> BuildArguments(string sourcePath, string destinationPath) =>
        [sourcePath, "-o", destinationPath];
}
