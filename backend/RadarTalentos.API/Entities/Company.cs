namespace RadarTalentos.API.Entities;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Nome em maiúsculas e sem espaços extras, para unicidade sem diferenciar caixa.</summary>
    public string NormalizedName { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Position> Positions { get; set; } = [];
}

public static class Modalities
{
    public static readonly string[] All = ["Presencial", "Home office", "Híbrida"];
}

public class Position
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Name { get; set; } = "";
    public string NormalizedName { get; set; } = "";
    public string? Requirements { get; set; }
    public string Modality { get; set; } = Modalities.All[0];
    public bool TravelAvailability { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Job> Jobs { get; set; } = [];
}
