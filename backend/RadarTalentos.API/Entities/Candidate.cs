namespace RadarTalentos.API.Entities;

public class Candidate
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? CurrentCompany { get; set; }
    public string? CurrentPosition { get; set; }
    /// <summary>Remuneração atual. Dado sigiloso (D9): fora das listagens comuns.</summary>
    public decimal? Salary { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? LinkedIn { get; set; }
    public string? Phone { get; set; }
    public string? PhoneOriginal { get; set; }
    /// <summary>Dado sensível (LGPD art. 11): fora das listagens comuns.</summary>
    public List<string> DiversityTags { get; set; } = [];
    public List<string> Restrictions { get; set; } = [];
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<CandidateOffLimit> OffLimits { get; set; } = [];
    public List<Application> Applications { get; set; } = [];
}

public class CandidateOffLimit
{
    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public Guid CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
