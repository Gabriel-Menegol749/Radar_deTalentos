using RadarTalentos.API.Entities;

namespace RadarTalentos.API.DTOs;

/// <summary>Recorte Empresa → Posição → Vaga usado por pipeline, métricas e exportações.</summary>
public record ScopeFilter(Guid? CompanyId, Guid? PositionId, Guid? JobId);

public record CreateApplicationRequest(
    Guid CandidateId,
    Guid JobId,
    string? Source,
    DateOnly? AccessedAt,
    bool OffLimitsOverride);

public record UpdateApplicationRequest(string? Source, string? Notes, decimal? OfferedSalary, decimal? RequestedSalary);

public record MoveApplicationRequest(string TargetStage, string? Notes, decimal? OfferedSalary, decimal? RequestedSalary);

public record ResolveApplicationRequest(ApplicationStatus Status, List<string>? Reasons, string? Notes);

public record PipelineCard(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    Guid JobId,
    string JobCode,
    string PositionName,
    string CompanyName,
    string Stage,
    string? Source,
    bool OffLimitsOverride,
    DateOnly AccessedAt);

public record HistoryDto(string Stage, ApplicationStatus Status, DateTime EnteredAt, DateTime? ExitedAt, string? ActorName, string? Notes);

public record ApplicationDetail(
    Guid Id,
    RefDto Candidate,
    Guid JobId,
    string JobCode,
    string PositionName,
    string CompanyName,
    JobStatus JobStatus,
    string? Source,
    string Stage,
    ApplicationStatus Status,
    List<string> RejectionReasons,
    decimal? OfferedSalary,
    decimal? RequestedSalary,
    DateOnly AccessedAt,
    string? Notes,
    bool OffLimitsOverride,
    string? OffLimitsOverrideBy,
    DateTime? OffLimitsOverrideAt,
    DateTime? ResolvedAt,
    List<HistoryDto> History);
