using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RadarTalentos.API.Services;

/// <summary>Normalizações herdadas do protótipo (LinkedIn, telefone, slug do código da vaga).</summary>
public static partial class TextNormalizer
{
    public static string RemoveDiacritics(string s)
    {
        var decomposed = s.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Chave de unicidade de nomes (empresa, posição): maiúsculas, espaços colapsados.</summary>
    public static string NormalizeName(string s) => MultiSpace().Replace(s.Trim(), " ").ToUpperInvariant();

    public static string? Clean(string? s)
    {
        if (s == null) return null;
        var t = MultiSpace().Replace(s.Trim(), " ");
        return t.Length == 0 ? null : t;
    }

    /// <summary>Parte do código da vaga: sem acentos, só A-Z0-9, cortada em <paramref name="len"/>.</summary>
    public static string SlugPart(string? s, int len)
    {
        if (string.IsNullOrWhiteSpace(s)) return "X";
        var output = NonAlnum().Replace(RemoveDiacritics(s).ToUpperInvariant(), "");
        if (output.Length > len) output = output[..len];
        return output.Length == 0 ? "X" : output;
    }

    public static string? NormalizeLinkedIn(string? raw)
    {
        var s = raw?.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        try { s = Uri.UnescapeDataString(s); } catch (UriFormatException) { /* mantém como veio */ }
        var m = LinkedInPath().Match(s);
        if (m.Success) return "linkedin.com/in/" + m.Groups[1].Value.TrimEnd('/').ToLowerInvariant();
        s = s.TrimStart('@');
        if (LinkedInHandle().IsMatch(s)) return "linkedin.com/in/" + s.ToLowerInvariant();
        return s;
    }

    public record PhoneResult(string? Value, string Label, bool Recognized);

    public static PhoneResult NormalizePhone(string? raw)
    {
        var s = raw?.Trim();
        if (string.IsNullOrEmpty(s)) return new(null, "", false);
        if (s.Contains("inmail", StringComparison.OrdinalIgnoreCase)) return new("inmail", "Inmail (sem telefone)", false);
        var digits = new string(s.Where(char.IsDigit).ToArray());
        if (digits.Length == 0) return new(s, "Não reconhecido como telefone", false);
        string ddd = "", rest;
        if (digits.Length is 10 or 11) { ddd = digits[..2]; rest = digits[2..]; }
        else if (digits.Length is 8 or 9) rest = digits;
        else return new(digits, $"{digits.Length} dígitos — confira", false);
        var formatted = rest.Length == 9 ? $"{rest[..5]}-{rest[5..]}" : $"{rest[..4]}-{rest[4..]}";
        return new((ddd.Length > 0 ? $"({ddd}) " : "") + formatted, "Padronizado", true);
    }

    /// <summary>Chave para comparar pessoas por nome: sem acentos, minúsculas, espaços colapsados.</summary>
    public static string NameKey(string? name) =>
        string.IsNullOrWhiteSpace(name) ? "" : MultiSpace().Replace(RemoveDiacritics(name).Trim().ToLowerInvariant(), " ");

    /// <summary>Últimos 8 dígitos de um telefone reconhecido (ignora DDD e o nono dígito); null se não for telefone.</summary>
    public static string? PhoneKey(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone) || phone.Contains("inmail", StringComparison.OrdinalIgnoreCase)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 8 ? digits[^8..] : null;
    }

    [GeneratedRegex(@"\s+")] private static partial Regex MultiSpace();
    [GeneratedRegex("[^A-Z0-9]+")] private static partial Regex NonAlnum();
    [GeneratedRegex(@"linkedin\.com/in/([^/?&#\s]+)", RegexOptions.IgnoreCase)] private static partial Regex LinkedInPath();
    [GeneratedRegex(@"^[\p{L}\p{N}\-._]+$")] private static partial Regex LinkedInHandle();
}
