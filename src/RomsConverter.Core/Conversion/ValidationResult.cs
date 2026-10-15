namespace RomsConverter.Core.Conversion;

public sealed record ValidationResult(bool IsValid, IReadOnlyList<string> Errors);
