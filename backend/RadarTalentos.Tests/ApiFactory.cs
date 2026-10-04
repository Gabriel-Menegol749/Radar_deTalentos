using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;

namespace RadarTalentos.Tests;

/// <summary>
/// Sobe a API contra um banco de testes descartável. A connection string vem de RADAR_TEST_DB
/// (padrão: banco radar_talentos_test no localhost:5433). Usa apenas dados sintéticos.
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@teste.local";
    public const string AdminPassword = "Admin@12345";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("RADAR_TEST_DB")
        ?? "Host=localhost;Port=5433;Database=radar_talentos_test;Username=postgres";

    public ApiFactory()
    {
        // Banco limpo a cada execução da suíte; a API aplica as migrations ao subir.
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(ConnectionString).Options);
        db.Database.EnsureDeleted();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", ConnectionString);
        builder.UseSetting("Jwt:Key", "chave-de-teste-com-mais-de-32-caracteres-0123456789");
        builder.UseSetting("Seed:AdminEmail", AdminEmail);
        builder.UseSetting("Seed:AdminPassword", AdminPassword);
    }

    public async Task<HttpClient> LoginAsync(string email = AdminEmail, string password = AdminPassword)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        res.EnsureSuccessStatusCode();
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;

public static class HttpExtensions
{
    public static async Task<JsonElement> PostJson(this HttpClient c, string url, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var res = await c.PostAsJsonAsync(url, body, ApiFactory.Json);
        return await Read(res, expected);
    }

    public static async Task<JsonElement> PutJson(this HttpClient c, string url, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var res = await c.PutAsJsonAsync(url, body, ApiFactory.Json);
        return await Read(res, expected);
    }

    public static async Task<JsonElement> GetJson(this HttpClient c, string url, HttpStatusCode expected = HttpStatusCode.OK) =>
        await Read(await c.GetAsync(url), expected);

    private static async Task<JsonElement> Read(HttpResponseMessage res, HttpStatusCode expected)
    {
        var text = await res.Content.ReadAsStringAsync();
        Assert.True(res.StatusCode == expected, $"Esperado {(int)expected}, veio {(int)res.StatusCode}: {text}");
        return string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    public static string Str(this JsonElement e, string prop) => e.GetProperty(prop).ToString();
    public static Guid Id(this JsonElement e) => e.GetProperty("id").GetGuid();
}
