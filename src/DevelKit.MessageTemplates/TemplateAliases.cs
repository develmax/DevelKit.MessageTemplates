using System.Text.RegularExpressions;

namespace DevelKit.MessageTemplates;

/// <summary>Имя вида {{entity:field}}, выражение для подстановки и признак активности.</summary>
public sealed record TemplateAlias(string Name, string Expansion, bool IsActive = true);

/// <summary>Раскрывает каталог за один проход, без повторной обработки вставленных выражений.</summary>
public static class TemplateAliases
{
    private static readonly Regex Pattern = new(
        @"\{\{\s*(?<entity>\w+)\s*:\s*(?<field>\w+)\s*\}\}",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static string Expand(string text, IEnumerable<TemplateAlias> aliases)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var alias in aliases)
        {
            var match = Pattern.Match(alias.Name);
            if (!match.Success || match.Length != alias.Name.Length)
                throw new ArgumentException($"Invalid alias: {alias.Name}");
            // Последнее определение имени побеждает. Неактивное определение удаляет вхождение.
            values[Key(match)] = alias.IsActive ? alias.Expansion : string.Empty;
        }
        return Pattern.Replace(text, match => values.TryGetValue(Key(match), out var value) ? value : match.Value);
    }

    private static string Key(Match match) => match.Groups["entity"].Value + "." + match.Groups["field"].Value;
}