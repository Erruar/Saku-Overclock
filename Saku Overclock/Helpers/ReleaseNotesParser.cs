using System.Text;
using System.Text.RegularExpressions;

namespace Saku_Overclock.Helpers;

public static partial class ReleaseNotesParser
{
    /// <summary>
    ///     Parses GitHub release body to extract key highlighted sections
    /// </summary>
    public static string ParseReleaseNotes(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return "**No release notes available.**";
        }

        var result = new StringBuilder();
        var lines = rawBody.Replace("\r\n", "\n").Split('\n');

        string? currentSection = null;
        var currentItems = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // 1. - **Section Name:**
            var sectionMatch = SectionRegex().Match(line); 
            if (sectionMatch.Success)
            {
                // Save section
                if (currentSection != null && currentItems.Count > 0)
                {
                    AppendSection(result, currentSection, currentItems);
                    currentItems.Clear();
                }
                
                // Build new section
                currentSection = sectionMatch.Groups["section"].Value.Trim();
                continue;
            }

            // 2. - **Item:** Description
            if (currentSection != null)
            {
                var itemMatch = ItemRegex().Match(line);
                if (itemMatch.Success)
                {
                    var title = itemMatch.Groups["title"].Value.Trim();
                    var desc = itemMatch.Groups["desc"].Value.Trim();
                    
                    // Remove :: without description
                    var itemText = string.IsNullOrWhiteSpace(desc) ? title : $"{title}: {desc}";
                    currentItems.Add(itemText);
                    continue;
                }

                // 3. Section end
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    if (currentItems.Count > 0)
                    {
                        AppendSection(result, currentSection, currentItems);
                    }
                    currentSection = null;
                    currentItems.Clear();
                }
            }
        }

        // Save last section
        if (currentSection != null && currentItems.Count > 0)
        {
            AppendSection(result, currentSection, currentItems);
        }

        return result.Length > 0 ? "UpdateReleaseNotes".GetLocalized() + result.ToString().TrimEnd() 
            : "**No key highlights found in this release.**";
    }

    /// <summary>
    ///     Appends a formatted section to the StringBuilder
    /// </summary>
    private static void AppendSection(StringBuilder sb, string sectionName, List<string> items)
    {
        if (items.Count == 0) return;

        sb.AppendLine($"▸ {sectionName}:");
        foreach (var item in items)
        {
            sb.AppendLine($"⠀• {item}");
        }
        sb.AppendLine();
    }

    /// <summary>
    ///     Matches section headers:
    ///     - **Section Name:**
    ///     - **Section Name**:
    /// </summary>
    [GeneratedRegex(
        @"^\s*[-*+]\s+\*\*(?<section>.+?)(?::\*\*|\*\*:)\s*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SectionRegex();

    /// <summary>
    ///     Matches section items:
    ///     - **Item Name:** Description
    ///     - **Item Name**: Description
    /// </summary>
    [GeneratedRegex(
        @"^\s*[-*+]\s+\*\*(?<title>.+?)(?::\*\*|\*\*:)\s*(?<desc>.*)$",
        RegexOptions.CultureInvariant)]
    private static partial Regex ItemRegex();
}