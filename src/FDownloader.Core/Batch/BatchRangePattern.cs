using System.Globalization;
using System.Text.RegularExpressions;

namespace FDownloader.Core.Batch;

/// <summary>
/// Sayısal ve alfabetik aralık desenlerini genişleten motor.
/// Desteklenen formatlar:
/// - [1-10] (1'den 10'a tekil basamak)
/// - [01-20] (2 basamaklı sıfır dolgulu)
/// - [001-100:2] (adım miktarlı, 1, 3, 5...)
/// - [10-1] (azalan aralık)
/// - [a-z], [A-Z], [z-a] (alfabetik aralık)
/// </summary>
public static partial class BatchRangePattern
{
    // [01-20] veya [1-100:2] veya [a-z] desenini yakalar
    private static readonly Regex PatternRegex = new(
        @"\[(?<start>[0-9]+|[a-zA-Z])-(?<end>[0-9]+|[a-zA-Z])(?::(?<step>[0-9]+))?\]",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    /// <summary>
    /// Bir metin veya URL'nin aralık deseni içerip içermediğini kontrol eder.
    /// </summary>
    public static bool HasPattern(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return PatternRegex.IsMatch(input);
    }

    /// <summary>
    /// Aralık deseni içeren bir şablon URL'yi çoklu URL dizisine genişletir.
    /// Eğer birden fazla aralık varsa Kartezyen çarpım (Cartesian Product) olarak genişletilir.
    /// </summary>
    public static List<string> ExpandPattern(string template, int maxItems = 5000)
    {
        if (string.IsNullOrWhiteSpace(template))
            return new List<string>();

        var matches = PatternRegex.Matches(template);
        if (matches.Count == 0)
        {
            return new List<string> { template.Trim() };
        }

        List<string> currentResults = new() { template };

        foreach (Match match in matches)
        {
            var matchText = match.Value;
            var startStr = match.Groups["start"].Value;
            var endStr = match.Groups["end"].Value;
            var stepStr = match.Groups["step"].Success ? match.Groups["step"].Value : "1";

            if (!int.TryParse(stepStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var step) || step <= 0)
            {
                step = 1;
            }

            var expansions = GenerateSubRange(startStr, endStr, step);
            if (expansions.Count == 0)
            {
                // Geçersiz aralık ise olduğu gibi bırak
                continue;
            }

            var nextResults = new List<string>();
            foreach (var current in currentResults)
            {
                // Yalnızca ilk karşılaşılan hedef eşleşmeyi değiştir (çoklu desen desteği için)
                var index = current.IndexOf(matchText, StringComparison.Ordinal);
                if (index < 0)
                {
                    nextResults.Add(current);
                    continue;
                }

                foreach (var exp in expansions)
                {
                    if (nextResults.Count >= maxItems)
                        break;

                    var replaced = string.Concat(current.AsSpan(0, index), exp, current.AsSpan(index + matchText.Length));
                    nextResults.Add(replaced);
                }

                if (nextResults.Count >= maxItems)
                    break;
            }

            currentResults = nextResults;
            if (currentResults.Count >= maxItems)
                break;
        }

        return currentResults;
    }

    private static List<string> GenerateSubRange(string startStr, string endStr, int step)
    {
        var result = new List<string>();

        // Sayısal aralık: [01-20]
        if (long.TryParse(startStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var startNum) &&
            long.TryParse(endStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var endNum))
        {
            var padLength = Math.Max(startStr.Length, endStr.Length);
            var isPadded = startStr.StartsWith('0') && startStr.Length > 1;

            if (startNum <= endNum)
            {
                for (var i = startNum; i <= endNum; i += step)
                {
                    var numStr = i.ToString(CultureInfo.InvariantCulture);
                    result.Add(isPadded ? numStr.PadLeft(padLength, '0') : numStr);
                }
            }
            else
            {
                for (var i = startNum; i >= endNum; i -= step)
                {
                    var numStr = i.ToString(CultureInfo.InvariantCulture);
                    result.Add(isPadded ? numStr.PadLeft(padLength, '0') : numStr);
                }
            }

            return result;
        }

        // Alfabetik aralık: [a-z], [A-Z], [z-a]
        if (startStr.Length == 1 && endStr.Length == 1 &&
            char.IsLetter(startStr[0]) && char.IsLetter(endStr[0]))
        {
            var startChar = startStr[0];
            var endChar = endStr[0];

            if (startChar <= endChar)
            {
                for (var c = startChar; c <= endChar; c = (char)(c + step))
                {
                    result.Add(c.ToString());
                }
            }
            else
            {
                for (var c = startChar; c >= endChar; c = (char)(c - step))
                {
                    result.Add(c.ToString());
                }
            }

            return result;
        }

        return result;
    }
}
