using RadarTalentos.API.Entities;

namespace RadarTalentos.API.DTOs;

/// <summary>Linha já mapeada no frontend. RowNumber é a linha original na planilha (rastreabilidade).</summary>
public record ImportRow(
    int RowNumber,
    string? Name,
    string? LinkedIn,
    string? Phone,
    string? CurrentCompany,
    string? CurrentPosition,
    string? Source,
    decimal? CurrentSalary,
    decimal? RequestedSalary,
    string? Notes);

public record ImportPreviewRequest(List<Guid> JobIds, List<ImportRow> Rows);

public enum ImportMatch { New, ExactMatch, Conflict, DuplicateInFile, Invalid }

public record ImportDifference(string Field, string Label, string? FileValue, string? DbValue);

public record ImportPreviewRow(
    int RowNumber,
    string? Name,
    string? LinkedIn,
    string? Phone,
    string? PhoneWarning,
    ImportMatch Match,
    Guid? ExistingCandidateId,
    string? ExistingCandidateName,
    int? DuplicateOfRow,
    List<ImportDifference> Differences,
    List<Guid> AlreadyInJobIds,
    List<Guid> OffLimitsCompanyIds,
    List<string> Warnings);

public record ImportPreviewResponse(List<ImportPreviewRow> Rows, int New, int ExactMatch, int Conflict, int DuplicateInFile, int Invalid);

public enum ImportResolution { Update, KeepExisting }

public record ImportConfirmRow(
    ImportRow Row,
    bool Include,
    ImportResolution? Resolution,
    Guid JobId,
    string? Stage,
    ApplicationStatus? Status,
    bool OffLimitsOverride);

public record ImportConfirmRequest(List<ImportConfirmRow> Rows, DateOnly? AccessedAt);

public record ImportRowOutcome(int RowNumber, string Outcome, string? Reason);

public record ImportConfirmResponse(
    int CandidatesCreated,
    int CandidatesUpdated,
    int CandidatesKept,
    int ApplicationsCreated,
    int Skipped,
    List<ImportRowOutcome> Rows);
