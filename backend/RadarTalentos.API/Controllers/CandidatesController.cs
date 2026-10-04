using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;
using RadarTalentos.API.Infrastructure;
using RadarTalentos.API.Services;

namespace RadarTalentos.API.Controllers;

[ApiController]
[Route("api/candidates")]
public class CandidatesController(AppDbContext db) : ControllerBase
{
    /// <summary>Listagem comum: sem diversidade e sem remuneração (D9).</summary>
    [HttpGet]
    public async Task<PagedResult<CandidateListItem>> List([FromQuery] CandidateFilter filter)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);
        var q = ExportService.FilterCandidates(db.Candidates.AsNoTracking(), filter);
        var total = await q.CountAsync();
        var items = await q.OrderByDescending(c => c.CreatedAt).ThenBy(c => c.Name)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CandidateListItem(
                c.Id, c.Name, c.CurrentCompany, c.CurrentPosition, c.City, c.State, c.LinkedIn, c.Phone,
                c.Restrictions,
                c.OffLimits.Select(o => new RefDto(o.CompanyId, o.Company.Name)).ToList(),
                c.Applications.Count(a => a.Status == ApplicationStatus.Active),
                c.CreatedAt))
            .ToListAsync();
        return new PagedResult<CandidateListItem>(items, total, page, pageSize);
    }

    [HttpGet("{id:guid}")]
    public async Task<CandidateDetail> Get(Guid id)
    {
        var c = await db.Candidates.AsNoTracking()
            .Include(x => x.OffLimits).ThenInclude(o => o.Company)
            .Include(x => x.Applications).ThenInclude(a => a.Job).ThenInclude(j => j.Position).ThenInclude(p => p.Company)
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive) ?? throw AppException.NotFound("Candidato");
        return new CandidateDetail(
            c.Id, c.Name, c.CurrentCompany, c.CurrentPosition, c.Salary, c.City, c.State, c.LinkedIn, c.Phone, c.PhoneOriginal,
            c.DiversityTags, c.Restrictions,
            c.OffLimits.Select(o => new RefDto(o.CompanyId, o.Company.Name)).ToList(),
            c.Notes, c.CreatedAt, c.UpdatedAt,
            c.Applications.OrderByDescending(a => a.AccessedAt).Select(a => new CandidateApplicationSummary(
                a.Id, a.JobId, a.Job.Code, a.Job.Position.Name, a.Job.Position.Company.Name, a.CurrentStage, a.Status, a.AccessedAt)).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<CandidateDetail>> Create(CandidateInput input)
    {
        var candidate = new Candidate();
        await Apply(candidate, input);
        db.Candidates.Add(candidate);
        await db.SaveChangesAsync();
        return await Get(candidate.Id);
    }

    [HttpPut("{id:guid}")]
    public async Task<CandidateDetail> Update(Guid id, CandidateInput input)
    {
        var candidate = await db.Candidates.Include(c => c.OffLimits).FirstOrDefaultAsync(c => c.Id == id && c.IsActive)
            ?? throw AppException.NotFound("Candidato");
        await Apply(candidate, input);
        candidate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return await Get(id);
    }

    /// <summary>Inativa (D8): o histórico de prospecções é preservado.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var candidate = await db.Candidates.FindAsync(id) ?? throw AppException.NotFound("Candidato");
        candidate.IsActive = false;
        candidate.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task Apply(Candidate c, CandidateInput input)
    {
        c.Name = TextNormalizer.Clean(input.Name) ?? throw AppException.BadRequest("Informe o nome do candidato.");
        if (input.Salary is < 0) throw AppException.BadRequest("Remuneração inválida.");
        var state = TextNormalizer.Clean(input.State)?.ToUpperInvariant();
        if (state != null && state.Length != 2) throw AppException.BadRequest("UF deve ter 2 letras.");

        var linkedIn = TextNormalizer.NormalizeLinkedIn(input.LinkedIn);
        if (linkedIn != null)
        {
            var other = await db.Candidates.AsNoTracking()
                .FirstOrDefaultAsync(x => x.LinkedIn == linkedIn && x.IsActive && x.Id != c.Id);
            if (other != null) throw AppException.Conflict($"Já existe um candidato com este LinkedIn: {other.Name}.", "DUPLICATE_LINKEDIN");
        }
        var phone = TextNormalizer.NormalizePhone(input.Phone);

        c.CurrentCompany = TextNormalizer.Clean(input.CurrentCompany);
        c.CurrentPosition = TextNormalizer.Clean(input.CurrentPosition);
        c.Salary = input.Salary;
        c.City = TextNormalizer.Clean(input.City);
        c.State = state;
        c.LinkedIn = linkedIn;
        c.Phone = phone.Value;
        c.PhoneOriginal = TextNormalizer.Clean(input.Phone);
        c.DiversityTags = Distinct(input.DiversityTags);
        c.Restrictions = Distinct(input.Restrictions);
        c.Notes = TextNormalizer.Clean(input.Notes);

        var wanted = (input.OffLimitCompanyIds ?? []).Distinct().ToList();
        if (wanted.Count > 0 && await db.Companies.CountAsync(x => wanted.Contains(x.Id)) != wanted.Count)
            throw AppException.BadRequest("Empresa off-limits inválida.");
        c.OffLimits.RemoveAll(o => !wanted.Contains(o.CompanyId));
        foreach (var companyId in wanted.Where(w => c.OffLimits.All(o => o.CompanyId != w)))
            c.OffLimits.Add(new CandidateOffLimit { CompanyId = companyId });
    }

    private static List<string> Distinct(List<string>? values) =>
        (values ?? []).Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList();
}
