namespace RadarTalentos.API.Entities;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Consultor = "Consultor";
    public static readonly string[] All = [Admin, Consultor];
}

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Consultor;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
