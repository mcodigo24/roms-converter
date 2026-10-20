namespace RomsConverter.Core.Archives;

public static class ArchiveFileTypes
{
    private static readonly string[] Extensions = [".zip", ".7z", ".rar"];

    public static bool IsArchive(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
}
