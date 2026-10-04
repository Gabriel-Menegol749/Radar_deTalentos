using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;

namespace RadarTalentos.API.Services;

public class MetricsService(AppDbContext db)
{
    public async Task<MetricsDto> Get(ScopeFilter filter)
    {
        var apps = PipelineService.ApplyScope(db.Applications.AsNoTracking(), filter);
        var jobs = PipelineService.ApplyScope(db.Jobs.AsNoTracking(), filter);

        // Status por GROUP BY no banco.
        var byStatus = await apps.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        int CountOf(ApplicationStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;
        var total = byStatus.Sum(x => x.Count);

        // Funil: maior etapa atingida no histórico, para que candidaturas encerradas continuem contando.
        var stagesReached = await db.ApplicationHistory.AsNoTracking()
            .Where(h => apps.Select(a => a.Id).Contains(h.ApplicationId))
            .GroupBy(h => new { h.ApplicationId, h.Stage })
            .Select(g => g.Key)
            .ToListAsync();
        var maxStage = stagesReached
            .GroupBy(x => x.ApplicationId)
            .Select(g => g.Max(x => Stages.IndexOf(x.Stage)))
            .ToList();
        var funnelCounts = Stages.All.Select((_, i) => maxStage.Count(m => m >= i)).ToList();
        var funnel = Stages.All.Select((stage, i) => new FunnelStep(
            stage,
            funnelCounts[i],
            i == 0 || funnelCounts[i - 1] == 0 ? null : (int)Math.Round(funnelCounts[i] * 100.0 / funnelCounts[i - 1]))).ToList();

        var openJobs = await jobs.CountAsync(j => j.Status == JobStatus.Open);
        var closed = await jobs.Where(j => j.Status == JobStatus.Closed && j.ClosedAt != null)
            .Select(j => new { j.OpenedAt, j.ClosedAt }).ToListAsync();
        int? avgClose = closed.Count == 0 ? null
            : (int)Math.Round(closed.Average(j => j.ClosedAt!.Value.DayNumber - j.OpenedAt.DayNumber));

        // Taxa de aceite: entre propostas respondidas (aceite = Hired; recusa = Declined na etapa Proposta).
        var hired = CountOf(ApplicationStatus.Hired);
        var refusedOffers = await apps.CountAsync(a => a.Status == ApplicationStatus.Declined && a.CurrentStage == Stages.Proposta);
        var answered = hired + refusedOffers;
        var declined = CountOf(ApplicationStatus.Declined);

        var sources = await apps.Where(a => a.Source != null && a.Source != "")
            .GroupBy(a => a.Source!)
            .Select(g => new { Source = g.Key, Total = g.Count(), Hired = g.Count(a => a.Status == ApplicationStatus.Hired) })
            .ToListAsync();

        var reasonLists = await apps.Where(a => a.Status == ApplicationStatus.Rejected)
            .Select(a => a.RejectionReasons).ToListAsync();
        var reasonCounts = reasonLists.SelectMany(r => r).GroupBy(r => r).ToDictionary(g => g.Key, g => g.Count());
        var catalog = await db.Options.AsNoTracking()
            .Where(o => o.Category == OptionCategories.RejectionReason && o.IsActive)
            .OrderBy(o => o.SortOrder).Select(o => o.Value).ToListAsync();
        var reasons = catalog.Concat(reasonCounts.Keys.Except(catalog))
            .Select(r => new ReasonMetric(r, reasonCounts.GetValueOrDefault(r))).ToList();

        // Oferta × pretensão, com a tolerância do protótipo: max(R$ 200, 5% da pretensão).
        var salaries = await apps.Where(a => a.OfferedSalary != null && a.RequestedSalary != null)
            .Select(a => new { Offered = a.OfferedSalary!.Value, Requested = a.RequestedSalary!.Value }).ToListAsync();
        int below = 0, compatible = 0, above = 0;
        foreach (var s in salaries)
        {
            var diff = s.Offered - s.Requested;
            var tolerance = Math.Max(200m, s.Requested * 0.05m);
            if (diff < -tolerance) below++;
            else if (diff > tolerance) above++;
            else compatible++;
        }

        return new MetricsDto(
            total,
            funnelCounts[Stages.IndexOf(Stages.Entrevista)],
            openJobs,
            avgClose,
            new RateDto(answered == 0 ? null : (int)Math.Round(hired * 100.0 / answered), hired, answered),
            new RateDto(total == 0 ? null : (int)Math.Round(declined * 100.0 / total), declined, total),
            funnel,
            sources.OrderByDescending(s => s.Total)
                .Select(s => new SourceMetric(s.Source, s.Total, s.Hired, s.Total == 0 ? 0 : (int)Math.Round(s.Hired * 100.0 / s.Total)))
                .ToList(),
            reasons,
            new OfferVsRequest(below, compatible, above, salaries.Count));
    }
}
