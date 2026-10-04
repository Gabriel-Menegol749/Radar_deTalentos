using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;
using RadarTalentos.API.Infrastructure;
using RadarTalentos.API.Services;

namespace RadarTalentos.API.Controllers;

[ApiController]
[Route("api/pipeline")]
public class PipelineController(AppDbContext db) : ControllerBase
{
    /// <summary>Somente prospecções ativas aparecem no Kanban (seção 5.3).</summary>
    [HttpGet]
    public async Task<List<PipelineCard>> Get([FromQuery] ScopeFilter filter) =>
        await PipelineService.ApplyScope(db.Applications.AsNoTracking(), filter)
            .Where(a => a.Status == ApplicationStatus.Active)
            .OrderBy(a => a.CreatedAt)
            .Select(a => new PipelineCard(
                a.Id, a.CandidateId, a.Candidate.Name, a.JobId, a.Job.Code, a.Job.Position.Name, a.Job.Position.Company.Name,
                a.CurrentStage, a.Source, a.OffLimitsOverride, a.AccessedAt))
            .ToListAsync();
}

[ApiController]
[Route("api/applications")]
public class ApplicationsController(AppDbContext db, PipelineService pipeline) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ApplicationDetail> Get(Guid id)
    {
        var a = await db.Applications.AsNoTracking()
            .Include(x => x.Candidate)
            .Include(x => x.Job).ThenInclude(j => j.Position).ThenInclude(p => p.Company)
            .Include(x => x.History).ThenInclude(h => h.Actor)
            .FirstOrDefaultAsync(x => x.Id == id) ?? throw AppException.NotFound("Prospecção");
        var overrideBy = a.OffLimitsOverrideByUserId.HasValue
            ? await db.Users.Where(u => u.Id == a.OffLimitsOverrideByUserId).Select(u => u.Name).FirstOrDefaultAsync()
            : null;
        return new ApplicationDetail(
            a.Id, new RefDto(a.CandidateId, a.Candidate.Name), a.JobId, a.Job.Code, a.Job.Position.Name, a.Job.Position.Company.Name,
            a.Job.Status, a.Source, a.CurrentStage, a.Status, a.RejectionReasons, a.OfferedSalary, a.RequestedSalary, a.AccessedAt,
            a.Notes, a.OffLimitsOverride, overrideBy, a.OffLimitsOverrideAt, a.ResolvedAt,
            a.History.OrderBy(h => h.EnteredAt).ThenBy(h => h.Status == ApplicationStatus.Active ? 0 : 1)
                .Select(h => new HistoryDto(h.Stage, h.Status, h.EnteredAt, h.ExitedAt, h.Actor?.Name, h.Notes)).ToList());
    }

    [HttpPost]
    public async Task<ApplicationDetail> Create(CreateApplicationRequest req)
    {
        var app = await pipeline.Create(req, User.UserId());
        return await Get(app.Id);
    }

    [HttpPut("{id:guid}")]
    public async Task<ApplicationDetail> Update(Guid id, UpdateApplicationRequest req)
    {
        await pipeline.Update(id, req);
        return await Get(id);
    }

    [HttpPost("{id:guid}/move")]
    public async Task<ApplicationDetail> Move(Guid id, MoveApplicationRequest req)
    {
        await pipeline.Move(id, req, User.UserId());
        return await Get(id);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<ApplicationDetail> Resolve(Guid id, ResolveApplicationRequest req)
    {
        await pipeline.Resolve(id, req, User.UserId());
        return await Get(id);
    }
}

[ApiController]
[Route("api/metrics")]
public class MetricsController(MetricsService metrics) : ControllerBase
{
    [HttpGet]
    public Task<MetricsDto> Get([FromQuery] ScopeFilter filter) => metrics.Get(filter);
}

[ApiController]
[Route("api/exports")]
public class ExportsController(ExportService exports) : ControllerBase
{
    private void EnsureAllowed(bool includeSensitive)
    {
        if (includeSensitive && !User.IsAdmin())
            throw new AppException(403, "FORBIDDEN", "Somente Admin pode exportar dados sensíveis.");
    }

    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates([FromQuery] CandidateFilter filter, [FromQuery] bool includeSensitive = false)
    {
        EnsureAllowed(includeSensitive);
        var bytes = await exports.Candidates(filter, includeSensitive);
        return File(bytes, ExportService.ContentType, $"radar_candidatos_{DateTime.Today:yyyy-MM-dd}.xlsx");
    }

    [HttpGet("pipeline")]
    public async Task<IActionResult> Pipeline([FromQuery] ScopeFilter filter, [FromQuery] bool includeSensitive = false)
    {
        EnsureAllowed(includeSensitive);
        var bytes = await exports.Pipeline(filter, includeSensitive);
        return File(bytes, ExportService.ContentType, $"radar_pipeline_{DateTime.Today:yyyy-MM-dd}.xlsx");
    }
}

[ApiController]
[Route("api/imports")]
[RequestSizeLimit(20_000_000)]
public class ImportsController(ImportService imports) : ControllerBase
{
    [HttpPost("preview")]
    public Task<ImportPreviewResponse> Preview(ImportPreviewRequest req) => imports.Preview(req);

    [HttpPost("confirm")]
    public Task<ImportConfirmResponse> Confirm(ImportConfirmRequest req) => imports.Confirm(req, User.UserId());
}
