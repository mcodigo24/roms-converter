namespace RomsConverter.Core.Conversion;

public interface IJobValidator
{
    ValidationResult Validate(ConversionJob job);
}
