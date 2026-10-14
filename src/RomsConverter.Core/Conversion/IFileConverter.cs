namespace RomsConverter.Core.Conversion;

public interface IFileConverter
{
    Task ConvertAsync(string sourcePath, string destinationPath, CancellationToken cancellationToken = default);
}
