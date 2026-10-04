using RadarTalentos.API.Entities;

namespace RadarTalentos.API.DTOs;

public record CandidateInput(
    string Name,
    string? CurrentCompany,
    string? CurrentPosition,
    decimal? Salary,
    string? City,
    string? State,
    string? LinkedIn,
    string? Phone,
    List<string>? DiversityTags,
    List<string>? Restrictions,
    List<Guid>? OffLimitCompanyIds,
    string? Notes);

public record CandidateFilter(string? Search, string? State, string? Company, int Page = 1, int PageSize = 50);

public record RefDto(Guid Id, string Name);

/// <summary>Item de listagem: sem diversidade e sem remuneração (D9).</summary>
public record CandidateListItem(
    Guid Id,
    string Name,
    string? CurrentCompany,
    string? CurrentPosition,
    string? City,
    string? State,
    string? LinkedIn,
    string? Phone,
    List<string> Restrictions,
    List<RefDto> OffLimits,
    int ActiveApplications,
    DateTime CreatedAt);

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);

public record CandidateApplicationSummary(
    Guid Id, Guid JobId, string JobCode, string PositionName, string CompanyName,
    string Stage, ApplicationStatus Status, DateOnly AccessedAt);

public record CandidateDetail(
    Guid Id,
    string Name,
    string? CurrentCompany,
    string? CurrentPosition,
    decimal? Salary,
    string? City,
    string? State,
    string? LinkedIn,
    string? Phone,
    string? PhoneOriginal,
    List<string> DiversityTags,
    List<string> Restrictions,
    List<RefDto> OffLimits,
    string? Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    List<CandidateApplicationSummary> Applications);
