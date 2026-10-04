using RadarTalentos.API.Entities;

namespace RadarTalentos.API.DTOs;

public record CompanyInput(string Name);
public record CompanyDto(Guid Id, string Name, int JobsCount, int ApplicationsCount, DateTime CreatedAt);

public record PositionInput(Guid CompanyId, string Name, string? Requirements, string? Modality, bool TravelAvailability);
public record PositionDto(Guid Id, Guid CompanyId, string CompanyName, string Name, string? Requirements, string Modality, bool TravelAvailability);

/// <summary>Abre vaga a partir de posição existente (PositionId) ou cria/reaproveita posição pelo nome.</summary>
public record CreateJobRequest(
    Guid? PositionId,
    Guid? CompanyId,
    string? PositionName,
    string? Requirements,
    string? Modality,
    bool? TravelAvailability,
    DateOnly OpenedAt,
    string? Level,
    Guid? OwnerUserId);

public record JobDto(
    Guid Id,
    string Code,
    Guid PositionId,
    string PositionName,
    Guid CompanyId,
    string CompanyName,
    string? Level,
    JobStatus Status,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    int DaysOpen,
    Guid? OwnerUserId,
    string? OwnerName,
    string Modality,
    bool TravelAvailability,
    string? Requirements,
    int ApplicationsCount,
    string? ReopenedFromCode);

public record CodePreviewDto(string Code);

public record OptionDto(Guid Id, string Category, string Value);
public record OptionInput(string Category, string Value);
