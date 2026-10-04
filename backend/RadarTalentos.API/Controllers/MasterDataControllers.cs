using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;
using RadarTalentos.API.Infrastructure;
using RadarTalentos.API.Services;

namespace RadarTalentos.API.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<CompanyDto>> List() =>
        await db.Companies.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Name)
            .Select(c => new CompanyDto(
                c.Id, c.Name,
                c.Positions.SelectMany(p => p.Jobs).Count(),
                c.Positions.SelectMany(p => p.Jobs).SelectMany(j => j.Applications).Count(),
                c.CreatedAt))
            .ToListAsync();

    [HttpPost]
    public async Task<ActionResult<CompanyDto>> Create(CompanyInput input)
    {
        var name = TextNormalizer.Clean(input.Name) ?? throw AppException.BadRequest("Informe o nome da empresa.");
        var normalized = TextNormalizer.NormalizeName(name);
        var company = await db.Companies.FirstOrDefaultAsync(c => c.NormalizedName == normalized);
        if (company is { IsActive: true }) throw AppException.Conflict($"A empresa {company.Name} já está cadastrada.");
        if (company != null) company.IsActive = true; // reativa a empresa inativada (D8)
        else db.Companies.Add(company = new Company { Name = name, NormalizedName = normalized });
        await db.SaveChangesAsync();
        return new CompanyDto(company.Id, company.Name, 0, 0, company.CreatedAt);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, CompanyInput input)
    {
        var company = await db.Companies.FindAsync(id) ?? throw AppException.NotFound("Empresa");
        var name = TextNormalizer.Clean(input.Name) ?? throw AppException.BadRequest("Informe o nome da empresa.");
        company.Name = name;
        company.NormalizedName = TextNormalizer.NormalizeName(name);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var company = await db.Companies.FindAsync(id) ?? throw AppException.NotFound("Empresa");
        company.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}

[ApiController]
[Route("api/positions")]
public class PositionsController(AppDbContext db, JobService jobs) : ControllerBase
{
    private static PositionDto ToDto(Position p) =>
        new(p.Id, p.CompanyId, p.Company.Name, p.Name, p.Requirements, p.Modality, p.TravelAvailability);

    [HttpGet]
    public async Task<List<PositionDto>> List([FromQuery] Guid? companyId)
    {
        var q = db.Positions.AsNoTracking().Include(p => p.Company).Where(p => p.Company.IsActive);
        if (companyId.HasValue) q = q.Where(p => p.CompanyId == companyId);
        return (await q.OrderBy(p => p.Name).ToListAsync()).Select(ToDto).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<PositionDto>> Create(PositionInput input)
    {
        var position = await jobs.GetOrCreatePosition(input.CompanyId, input.Name, input.Requirements, input.Modality, input.TravelAvailability);
        await db.SaveChangesAsync();
        return ToDto(position);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PositionDto>> Update(Guid id, PositionInput input)
    {
        var position = await db.Positions.Include(p => p.Company).FirstOrDefaultAsync(p => p.Id == id) ?? throw AppException.NotFound("Posição");
        var name = TextNormalizer.Clean(input.Name) ?? throw AppException.BadRequest("Informe o nome da posição.");
        position.Name = name;
        position.NormalizedName = TextNormalizer.NormalizeName(name);
        position.Requirements = TextNormalizer.Clean(input.Requirements);
        if (input.Modality != null) position.Modality = JobService.ValidateModality(input.Modality);
        position.TravelAvailability = input.TravelAvailability;
        await db.SaveChangesAsync();
        return ToDto(position);
    }
}

[ApiController]
[Route("api/jobs")]
public class JobsController(AppDbContext db, JobService jobs) : ControllerBase
{
    private async Task<JobDto> Dto(Guid id)
    {
        var job = await jobs.Query().AsNoTracking().FirstOrDefaultAsync(j => j.Id == id) ?? throw AppException.NotFound("Vaga");
        return JobService.ToDto(job, await db.Applications.CountAsync(a => a.JobId == id));
    }

    [HttpGet]
    public async Task<List<JobDto>> List([FromQuery] Guid? companyId, [FromQuery] Guid? positionId, [FromQuery] JobStatus? status)
    {
        var q = PipelineService.ApplyScope(jobs.Query().AsNoTracking(), new ScopeFilter(companyId, positionId, null));
        if (status.HasValue) q = q.Where(j => j.Status == status);
        var list = await q.OrderByDescending(j => j.OpenedAt).ThenBy(j => j.Code).ToListAsync();
        var ids = list.Select(j => j.Id).ToList();
        var counts = await db.Applications.Where(a => ids.Contains(a.JobId))
            .GroupBy(a => a.JobId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        return list.Select(j => JobService.ToDto(j, counts.GetValueOrDefault(j.Id))).ToList();
    }

    [HttpGet("{id:guid}")]
    public Task<JobDto> Get(Guid id) => Dto(id);

    [HttpGet("code-preview")]
    public async Task<CodePreviewDto> CodePreview([FromQuery] Guid? positionId, [FromQuery] Guid? companyId, [FromQuery] string? positionName, [FromQuery] DateOnly openedAt) =>
        new(await jobs.PreviewCode(positionId, companyId, positionName, openedAt));

    [HttpPost]
    public async Task<ActionResult<JobDto>> Create(CreateJobRequest req)
    {
        Position position;
        if (req.PositionId.HasValue)
        {
            position = await db.Positions.Include(p => p.Company).FirstOrDefaultAsync(p => p.Id == req.PositionId)
                ?? throw AppException.NotFound("Posição");
            if (req.Requirements != null) position.Requirements = TextNormalizer.Clean(req.Requirements);
            if (req.Modality != null) position.Modality = JobService.ValidateModality(req.Modality);
            if (req.TravelAvailability.HasValue) position.TravelAvailability = req.TravelAvailability.Value;
        }
        else
        {
            if (!req.CompanyId.HasValue) throw AppException.BadRequest("Selecione a empresa.");
            position = await jobs.GetOrCreatePosition(req.CompanyId.Value, req.PositionName ?? "", req.Requirements, req.Modality, req.TravelAvailability);
        }
        var job = await jobs.CreateJob(position, req.OpenedAt, req.Level, req.OwnerUserId ?? User.UserId(), null);
        return await Dto(job.Id);
    }

    [HttpPost("{id:guid}/close")]
    public async Task<JobDto> Close(Guid id)
    {
        await jobs.Close(id);
        return await Dto(id);
    }

    [HttpPost("{id:guid}/reopen")]
    public async Task<JobDto> Reopen(Guid id)
    {
        var job = await jobs.Reopen(id, User.UserId());
        return await Dto(job.Id);
    }
}

[ApiController]
[Route("api/options")]
public class OptionsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<OptionDto>> List() =>
        await db.Options.AsNoTracking().Where(o => o.IsActive)
            .OrderBy(o => o.Category).ThenBy(o => o.SortOrder).ThenBy(o => o.Value)
            .Select(o => new OptionDto(o.Id, o.Category, o.Value)).ToListAsync();

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<OptionDto>> Create(OptionInput input)
    {
        if (!OptionCategories.All.Contains(input.Category)) throw AppException.BadRequest("Categoria inválida.");
        var value = TextNormalizer.Clean(input.Value) ?? throw AppException.BadRequest("Informe o valor.");
        var option = await db.Options.FirstOrDefaultAsync(o => o.Category == input.Category && o.Value == value);
        if (option is { IsActive: true }) throw AppException.Conflict("Esta opção já existe.");
        if (option != null) option.IsActive = true;
        else
        {
            var order = await db.Options.Where(o => o.Category == input.Category).MaxAsync(o => (int?)o.SortOrder) ?? -1;
            db.Options.Add(option = new OptionItem { Category = input.Category, Value = value, SortOrder = order + 1 });
        }
        await db.SaveChangesAsync();
        return new OptionDto(option.Id, option.Category, option.Value);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Deactivate(Guid id)
    {
        var option = await db.Options.FindAsync(id) ?? throw AppException.NotFound("Opção");
        option.IsActive = false;
        await db.SaveChangesAsync();
        return NoContent();
    }
}
