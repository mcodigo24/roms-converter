namespace RomsConverter.Core.Conversion;

public interface IFileConverter
{
    /// <param name="progress">Recibe porcentajes 0-100 mientras convierte; puede ser null.</param>
    Task ConvertAsync(
        string sourcePath,
        string destinationPath,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}
