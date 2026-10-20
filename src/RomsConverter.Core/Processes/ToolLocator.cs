namespace RomsConverter.Core.Processes;

/// <summary>Resuelve las herramientas externas empaquetadas en <c>&lt;carpeta del exe&gt;\tools\</c>.</summary>
public sealed class ToolLocator(string? baseDirectory = null)
{
    public string ToolsDirectory { get; } = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "tools");

    public string ChdmanPath => Path.Combine(ToolsDirectory, "chdman.exe");

    public string MaxcsoPath => Path.Combine(ToolsDirectory, "maxcso.exe");

    /// <summary>Rutas de las herramientas que faltan (vacío si están todas).</summary>
    public IReadOnlyList<string> FindMissing() =>
        new[] { ChdmanPath, MaxcsoPath }.Where(p => !File.Exists(p)).ToList();

    /// <summary>Mensaje accionable para mostrar al usuario cuando faltan herramientas; null si están todas.</summary>
    public string? DescribeMissing()
    {
        var missing = FindMissing();
        if (missing.Count == 0) return null;

        var names = string.Join(", ", missing.Select(Path.GetFileName));
        return $"Faltan herramientas de conversión ({names}) en \"{ToolsDirectory}\". " +
               "Es posible que el antivirus las haya bloqueado o eliminado: restaurá los archivos o reinstalá la aplicación.";
    }
}
