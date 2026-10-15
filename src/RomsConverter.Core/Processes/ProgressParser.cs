using System.Globalization;
using System.Text.RegularExpressions;

namespace RomsConverter.Core.Processes;

public static partial class ProgressParser
{
    /// <summary>
    /// Busca un porcentaje (ej. "45%", "45.3% complete", "45,3 %") en una línea de salida.
    /// Tolerante: si no hay porcentaje reconocible devuelve false.
    /// </summary>
    public static bool TryParsePercent(string line, out double percent)
    {
        percent = 0;
        var matches = PercentPattern().Matches(line);
        if (matches.Count == 0) return false;

        var text = matches[^1].Groups[1].Value.Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            return false;
        if (value is < 0 or > 100) return false;

        percent = value;
        return true;
    }

    [GeneratedRegex(@"(\d{1,3}(?:[.,]\d+)?)\s*%")]
    private static partial Regex PercentPattern();
}
