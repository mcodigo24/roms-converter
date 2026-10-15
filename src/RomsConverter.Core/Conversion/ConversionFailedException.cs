namespace RomsConverter.Core.Conversion;

public sealed class ConversionFailedException(string message) : Exception(message);
