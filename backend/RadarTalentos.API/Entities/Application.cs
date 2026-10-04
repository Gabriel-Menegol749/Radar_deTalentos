namespace RadarTalentos.API.Entities;

public enum ApplicationStatus { Active, Hired, Rejected, Declined }

/// <summary>Etapas do pipeline, na ordem do protótipo. Guardadas como texto no banco.</summary>
public static class Stages
{
    public const string Acessado = "Acessado";
    public const string Entrevista = "Entrevista";
    public const string Aprovacao = "Aprovação";
    public const string EtapaInterna = "Etapa interna";
    public const string Case = "Case";
    public const string Proposta = "Proposta";

    public static readonly string[] All = [Acessado, Entrevista, Aprovacao, EtapaInterna, Case, Proposta];

    public static int IndexOf(string stage) => Array.IndexOf(All, stage);
    public static bool IsValid(string? stage) => stage != null && IndexOf(stage) >= 0;
}

public class Application
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Candidate Candidate { get; set; } = null!;
    public Guid JobId { get; set; }
    public Job Job { get; set; } = null!;
    public string? Source { get; set; }
    public string CurrentStage { get; set; } = Stages.Acessado;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Active;
    public List<string> RejectionReasons { get; set; } = [];
    public decimal? OfferedSalary { get; set; }
    /// <summary>Expectativa/pretensão do candidato para este processo.</summary>
    public decimal? RequestedSalary { get; set; }
    public DateOnly AccessedAt { get; set; }
    /// <summary>Observações do processo (D4).</summary>
    public string? Notes { get; set; }
    public bool OffLimitsOverride { get; set; }
    public Guid? OffLimitsOverrideByUserId { get; set; }
    public DateTime? OffLimitsOverrideAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ApplicationHistory> History { get; set; } = [];
}

public class ApplicationHistory
{
    public Guid Id { get; set; }
    public Guid ApplicationId { get; set; }
    public Application Application { get; set; } = null!;
    public string Stage { get; set; } = Stages.Acessado;
    public ApplicationStatus Status { get; set; } = ApplicationStatus.Active;
    public DateTime EnteredAt { get; set; }
    public DateTime? ExitedAt { get; set; }
    public Guid? ActorUserId { get; set; }
    public User? Actor { get; set; }
    public string? Notes { get; set; }
}
