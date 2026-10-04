using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;
using RadarTalentos.API.Infrastructure;

namespace RadarTalentos.API.Services;

public class PipelineService(AppDbContext db)
{
    public const string FilledByAnotherReason = "Vaga preenchida por outro candidato";

    public static IQueryable<Application> ApplyScope(IQueryable<Application> q, ScopeFilter f)
    {
        if (f.JobId.HasValue) return q.Where(a => a.JobId == f.JobId);
        if (f.PositionId.HasValue) return q.Where(a => a.Job.PositionId == f.PositionId);
        if (f.CompanyId.HasValue) return q.Where(a => a.Job.Position.CompanyId == f.CompanyId);
        return q;
    }

    public static IQueryable<Job> ApplyScope(IQueryable<Job> q, ScopeFilter f)
    {
        if (f.JobId.HasValue) return q.Where(j => j.Id == f.JobId);
        if (f.PositionId.HasValue) return q.Where(j => j.PositionId == f.PositionId);
        if (f.CompanyId.HasValue) return q.Where(j => j.Position.CompanyId == f.CompanyId);
        return q;
    }

    public static DateTime EnteredAtFor(DateOnly date) =>
        date == DateOnly.FromDateTime(DateTime.Today) ? DateTime.UtcNow : date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc);

    /// <summary>D1: retorna as empresas off-limits do candidato que bloqueiam a vaga (vazio = liberado).</summary>
    public async Task<bool> IsOffLimits(Guid candidateId, Guid companyId) =>
        await db.CandidateOffLimits.AnyAsync(o => o.CandidateId == candidateId && o.CompanyId == companyId);

    public async Task<Application> Create(CreateApplicationRequest req, Guid actorUserId)
    {
        var candidate = await db.Candidates.FirstOrDefaultAsync(c => c.Id == req.CandidateId && c.IsActive)
            ?? throw AppException.NotFound("Candidato");
        var job = await db.Jobs.Include(j => j.Position).ThenInclude(p => p.Company).FirstOrDefaultAsync(j => j.Id == req.JobId)
            ?? throw AppException.NotFound("Vaga");
        if (job.Status != JobStatus.Open) throw AppException.BadRequest("A vaga está fechada.");
        if (await db.Applications.AnyAsync(a => a.CandidateId == candidate.Id && a.JobId == job.Id))
            throw AppException.Conflict($"{candidate.Name} já está no pipeline desta vaga.", "ALREADY_IN_JOB");

        var offLimits = await IsOffLimits(candidate.Id, job.Position.CompanyId);
        if (offLimits && !req.OffLimitsOverride)
            throw AppException.Conflict(
                $"{candidate.Name} está marcado como off-limits para {job.Position.Company.Name} (conflito de interesse).", "OFF_LIMITS");

        var accessedAt = req.AccessedAt ?? DateOnly.FromDateTime(DateTime.Today);
        var app = new Application
        {
            CandidateId = candidate.Id,
            JobId = job.Id,
            Source = TextNormalizer.Clean(req.Source),
            AccessedAt = accessedAt,
            OffLimitsOverride = offLimits,
            OffLimitsOverrideByUserId = offLimits ? actorUserId : null,
            OffLimitsOverrideAt = offLimits ? DateTime.UtcNow : null,
        };
        app.History.Add(new ApplicationHistory
        {
            Stage = Stages.Acessado,
            EnteredAt = EnteredAtFor(accessedAt),
            ActorUserId = actorUserId,
            Notes = offLimits ? "Override de off-limits confirmado pelo usuário." : null,
        });
        db.Applications.Add(app);
        await db.SaveChangesAsync();
        return app;
    }

    private async Task<Application> LoadActive(Guid id)
    {
        var app = await db.Applications.Include(a => a.History).FirstOrDefaultAsync(a => a.Id == id)
            ?? throw AppException.NotFound("Prospecção");
        if (app.Status != ApplicationStatus.Active) throw AppException.BadRequest("Esta prospecção já foi encerrada.");
        return app;
    }

    private static ApplicationHistory? OpenEntry(Application app) =>
        app.History.Where(h => h.ExitedAt == null).OrderByDescending(h => h.EnteredAt).FirstOrDefault();

    public async Task Update(Guid id, UpdateApplicationRequest req)
    {
        var app = await db.Applications.FindAsync(id) ?? throw AppException.NotFound("Prospecção");
        app.Source = TextNormalizer.Clean(req.Source);
        app.Notes = TextNormalizer.Clean(req.Notes);
        app.OfferedSalary = req.OfferedSalary;
        app.RequestedSalary = req.RequestedSalary;
        await db.SaveChangesAsync();
    }

    public async Task Move(Guid id, MoveApplicationRequest req, Guid actorUserId)
    {
        if (!Stages.IsValid(req.TargetStage)) throw AppException.BadRequest("Etapa inválida.");
        var app = await LoadActive(id);
        if (req.OfferedSalary.HasValue) app.OfferedSalary = req.OfferedSalary;
        if (req.RequestedSalary.HasValue) app.RequestedSalary = req.RequestedSalary;

        var now = DateTime.UtcNow;
        var current = OpenEntry(app);
        if (req.TargetStage == app.CurrentStage)
        {
            // Mesma etapa: só registra observações/remunerações, sem novo registro de histórico.
            if (current != null && !string.IsNullOrWhiteSpace(req.Notes))
                current.Notes = string.Join("\n", new[] { current.Notes, req.Notes.Trim() }.Where(s => !string.IsNullOrEmpty(s)));
        }
        else
        {
            var backwards = Stages.IndexOf(req.TargetStage) < Stages.IndexOf(app.CurrentStage);
            if (current != null) current.ExitedAt = now;
            db.ApplicationHistory.Add(new ApplicationHistory
            {
                ApplicationId = app.Id,
                Stage = req.TargetStage,
                EnteredAt = now,
                ActorUserId = actorUserId,
                Notes = TextNormalizer.Clean(req.Notes) ?? (backwards ? "Retorno manual" : null),
            });
            app.CurrentStage = req.TargetStage;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Encerra a prospecção (D5). Hired fecha a vaga e encerra as demais ativas como Rejected (D6).</summary>
    public async Task Resolve(Guid id, ResolveApplicationRequest req, Guid actorUserId)
    {
        if (req.Status == ApplicationStatus.Active) throw AppException.BadRequest("Status de encerramento inválido.");
        var app = await LoadActive(id);
        await using var tx = await db.Database.BeginTransactionAsync();

        Close(app, req.Status, req.Reasons, req.Notes, actorUserId);
        if (req.Status == ApplicationStatus.Hired)
            await CloseJobAfterHire(app.JobId, app.Id, actorUserId);

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>D6: fecha a vaga e encerra as demais prospecções ativas como sem sucesso. Não salva.</summary>
    public async Task CloseJobAfterHire(Guid jobId, Guid hiredApplicationId, Guid actorUserId)
    {
        var job = await db.Jobs.FindAsync(jobId) ?? throw AppException.NotFound("Vaga");
        job.Status = JobStatus.Closed;
        job.ClosedAt = DateOnly.FromDateTime(DateTime.Today);
        var others = await db.Applications.Include(a => a.History)
            .Where(a => a.JobId == jobId && a.Id != hiredApplicationId && a.Status == ApplicationStatus.Active)
            .ToListAsync();
        foreach (var other in others)
            Close(other, ApplicationStatus.Rejected, [FilledByAnotherReason], null, actorUserId);
    }

    public void Close(Application app, ApplicationStatus status, List<string>? reasons, string? notes, Guid actorUserId)
    {
        var now = DateTime.UtcNow;
        var current = OpenEntry(app);
        if (current != null) current.ExitedAt = now;
        app.Status = status;
        app.ResolvedAt = now;
        app.RejectionReasons = (reasons ?? []).Select(r => r.Trim()).Where(r => r.Length > 0).Distinct().ToList();
        db.ApplicationHistory.Add(new ApplicationHistory
        {
            ApplicationId = app.Id,
            Stage = app.CurrentStage,
            Status = status,
            EnteredAt = now,
            ExitedAt = now,
            ActorUserId = actorUserId,
            Notes = TextNormalizer.Clean(notes),
        });
    }
}
