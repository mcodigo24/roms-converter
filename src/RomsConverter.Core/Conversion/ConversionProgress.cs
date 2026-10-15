namespace RomsConverter.Core.Conversion;

public sealed record ConversionProgress(Guid JobId, ConversionStatus Status, double? Percent, string? Message);
