using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;

namespace RadarTalentos.API.Services;

public class ExportService(AppDbContext db)
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly Dictionary<ApplicationStatus, string> StatusLabels = new()
    {
        [ApplicationStatus.Active] = "Em andamento",
        [ApplicationStatus.Hired] = "Contratado",
        [ApplicationStatus.Rejected] = "Reprovado / sem sucesso",
        [ApplicationStatus.Declined] = "Declínio",
    };

    /// <summary>Mesmo filtro da listagem de candidatos (GET /api/candidates), sem paginação.</summary>
    public static IQueryable<Candidate> FilterCandidates(IQueryable<Candidate> q, CandidateFilter f)
    {
        q = q.Where(c => c.IsActive);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var term = $"%{f.Search.Trim()}%";
            q = q.Where(c => EF.Functions.ILike(c.Name, term)
                || (c.CurrentCompany != null && EF.Functions.ILike(c.CurrentCompany, term))
                || (c.CurrentPosition != null && EF.Functions.ILike(c.CurrentPosition, term))
                || (c.City != null && EF.Functions.ILike(c.City, term))
                || (c.LinkedIn != null && EF.Functions.ILike(c.LinkedIn, term)));
        }
        if (!string.IsNullOrWhiteSpace(f.State)) q = q.Where(c => c.State == f.State.Trim().ToUpper());
        if (!string.IsNullOrWhiteSpace(f.Company))
            q = q.Where(c => c.CurrentCompany != null && EF.Functions.ILike(c.CurrentCompany, $"%{f.Company.Trim()}%"));
        return q;
    }

    public async Task<byte[]> Candidates(CandidateFilter filter, bool includeSensitive)
    {
        var list = await FilterCandidates(db.Candidates.AsNoTracking(), filter)
            .Include(c => c.OffLimits).ThenInclude(o => o.Company)
            .OrderBy(c => c.Name).ToListAsync();

        using var wb = new XLWorkbook();
        var headers = new List<string> { "Nome", "LinkedIn", "Contato", "Empresa atual", "Cargo atual", "Cidade", "UF", "Pontos restritivos", "Empresas off-limits", "Observações" };
        if (includeSensitive) headers.AddRange(["Remuneração atual", "Diversidade"]);
        var rows = list.Select(c =>
        {
            var row = new List<object?>
            {
                c.Name, c.LinkedIn, c.Phone, c.CurrentCompany, c.CurrentPosition, c.City, c.State,
                string.Join(", ", c.Restrictions), string.Join(", ", c.OffLimits.Select(o => o.Company.Name)), c.Notes,
            };
            if (includeSensitive) row.AddRange([c.Salary, string.Join(", ", c.DiversityTags)]);
            return row;
        });
        AddSheet(wb, "Candidatos", headers, rows);
        return Save(wb);
    }

    public async Task<byte[]> Pipeline(ScopeFilter filter, bool includeSensitive)
    {
        var apps = await PipelineService.ApplyScope(db.Applications.AsNoTracking(), filter)
            .Include(a => a.Candidate)
            .Include(a => a.Job).ThenInclude(j => j.Position).ThenInclude(p => p.Company)
            .OrderBy(a => a.Job.Code).ThenBy(a => a.Candidate.Name)
            .ToListAsync();

        var jobs = await PipelineService.ApplyScope(db.Jobs.AsNoTracking(), filter)
            .Include(j => j.Position).ThenInclude(p => p.Company).Include(j => j.Owner)
            .OrderByDescending(j => j.OpenedAt).ToListAsync();

        using var wb = new XLWorkbook();
        var headers = new List<string>
        {
            "Candidato", "Vaga", "Empresa", "Posição", "Etapa", "Status", "Fonte", "Data de acesso",
            "Motivos", "Off-limits (override)", "Observações", "Encerrada em",
        };
        if (includeSensitive) headers.AddRange(["Remuneração oferecida", "Expectativa"]);
        AddSheet(wb, "Prospecções", headers, apps.Select(a =>
        {
            var row = new List<object?>
            {
                a.Candidate.Name, a.Job.Code, a.Job.Position.Company.Name, a.Job.Position.Name, a.CurrentStage,
                StatusLabels[a.Status], a.Source, a.AccessedAt.ToDateTime(TimeOnly.MinValue),
                string.Join(", ", a.RejectionReasons), a.OffLimitsOverride ? "Sim" : "", a.Notes,
                a.ResolvedAt?.ToLocalTime(),
            };
            if (includeSensitive) row.AddRange([a.OfferedSalary, a.RequestedSalary]);
            return row;
        }));

        var today = DateOnly.FromDateTime(DateTime.Today);
        AddSheet(wb, "Vagas",
            ["Código", "Empresa", "Posição", "Nível", "Data abertura", "Status", "Data fechamento", "Dias em aberto/até fechar", "Responsável", "Candidatos"],
            jobs.Select(j => new List<object?>
            {
                j.Code, j.Position.Company.Name, j.Position.Name, j.Level, j.OpenedAt.ToDateTime(TimeOnly.MinValue),
                j.Status == JobStatus.Closed ? "Fechada" : "Aberta", j.ClosedAt?.ToDateTime(TimeOnly.MinValue),
                ((j.Status == JobStatus.Closed && j.ClosedAt.HasValue ? j.ClosedAt.Value : today).DayNumber - j.OpenedAt.DayNumber),
                j.Owner?.Name, apps.Count(a => a.JobId == j.Id),
            }));
        return Save(wb);
    }

    private static void AddSheet(XLWorkbook wb, string name, IList<string> headers, IEnumerable<IList<object?>> rows)
    {
        var ws = wb.Worksheets.Add(name);
        for (var i = 0; i < headers.Count; i++) ws.Cell(1, i + 1).Value = headers[i];
        var r = 2;
        foreach (var row in rows)
        {
            for (var i = 0; i < row.Count; i++)
            {
                var cell = ws.Cell(r, i + 1);
                switch (row[i])
                {
                    case null: break;
                    case DateTime d: cell.Value = d; cell.Style.DateFormat.Format = "dd/MM/yyyy"; break;
                    case decimal m: cell.Value = m; cell.Style.NumberFormat.Format = "#,##0.00"; break;
                    case int n: cell.Value = n; break;
                    default: cell.Value = row[i]!.ToString(); break;
                }
            }
            r++;
        }
        var header = ws.Range(1, 1, 1, headers.Count);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2EBE6");
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents(1, Math.Min(r, 200), 8, 60);
    }

    private static byte[] Save(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
