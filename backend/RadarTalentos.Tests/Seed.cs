using System.Text.Json;

namespace RadarTalentos.Tests;

/// <summary>Cria dados sintéticos com nomes únicos por teste.</summary>
public static class Seed
{
    public static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..6]}";

    public static Task<JsonElement> Company(HttpClient c, string? name = null) =>
        c.PostJson("/api/companies", new { name = name ?? Unique("Empresa") });

    public static Task<JsonElement> Job(HttpClient c, Guid companyId, string? position = null, string openedAt = "2026-07-01") =>
        c.PostJson("/api/jobs", new { companyId, positionName = position ?? Unique("Analista"), openedAt });

    public static Task<JsonElement> Candidate(HttpClient c, object? overrides = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"] = Unique("Pessoa"),
            ["currentCompany"] = "Empresa Atual",
            ["phone"] = "51 99999-0000",
            ["salary"] = 10000,
            ["diversityTags"] = new[] { "PCD" },
        };
        if (overrides != null)
            foreach (var p in overrides.GetType().GetProperties()) body[p.Name] = p.GetValue(overrides);
        return c.PostJson("/api/candidates", body);
    }

    public static Task<JsonElement> Application(HttpClient c, Guid candidateId, Guid jobId, string source = "Hunting") =>
        c.PostJson("/api/applications", new { candidateId, jobId, source });
}
