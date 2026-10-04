using Npgsql;

namespace RadarTalentos.API.Infrastructure;

public static class ConnectionStringResolver
{
    /// <summary>
    /// Usa DATABASE_URL (formato postgres://usuario:senha@host:porta/banco, padrão do Render) quando presente;
    /// caso contrário, ConnectionStrings:Default.
    /// </summary>
    public static string Resolve(IConfiguration config)
    {
        var url = config["DATABASE_URL"];
        if (!string.IsNullOrWhiteSpace(url))
        {
            var uri = new Uri(url);
            var userInfo = uri.UserInfo.Split(':', 2);
            return new NpgsqlConnectionStringBuilder
            {
                Host = uri.Host,
                Port = uri.Port > 0 ? uri.Port : 5432,
                Username = Uri.UnescapeDataString(userInfo[0]),
                Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
                Database = uri.AbsolutePath.TrimStart('/'),
                SslMode = SslMode.Prefer,
            }.ConnectionString;
        }

        return config.GetConnectionString("Default")
            ?? throw new InvalidOperationException("Configure ConnectionStrings:Default ou DATABASE_URL.");
    }
}
