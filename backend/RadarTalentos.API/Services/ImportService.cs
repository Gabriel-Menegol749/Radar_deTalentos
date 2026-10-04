using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;
using RadarTalentos.API.Infrastructure;

namespace RadarTalentos.API.Services;

/// <summary>
/// Importação de planilha (seção 5.1): identifica candidatos já existentes por LinkedIn ou Nome+Telefone,
/// nunca sobrescreve sem decisão do usuário e nunca descarta uma linha sem informar o motivo.
/// </summary>
public class ImportService(AppDbContext db, PipelineService pipeline)
{
    private sealed record Existing(Guid Id, string Name, string? LinkedIn, string? Phone, string? CurrentCompany, string? CurrentPosition, decimal? Salary);

    /// <summary>Resultado da identificação de uma linha, compartilhado por preview e confirm.</summary>
    private sealed record Identified(
        ImportRow Row, string? Name, string? LinkedIn, TextNormalizer.PhoneResult Phone,
        ImportMatch Match, Existing? Existing, int? DuplicateOfRow, List<ImportDifference> Differences, List<string> Warnings);

    private static List<string> Keys(string? linkedIn, string? name, string? phone)
    {
        var keys = new List<string>();
        if (linkedIn != null) keys.Add("li:" + linkedIn);
        var phoneKey = TextNormalizer.PhoneKey(phone);
        if (phoneKey != null && !string.IsNullOrWhiteSpace(name)) keys.Add($"np:{TextNormalizer.NameKey(name)}|{phoneKey}");
        return keys;
    }

    private async Task<List<Identified>> Identify(IEnumerable<ImportRow> rows)
    {
        var existing = await db.Candidates.AsNoTracking().Where(c => c.IsActive)
            .Select(c => new Existing(c.Id, c.Name, c.LinkedIn, c.Phone, c.CurrentCompany, c.CurrentPosition, c.Salary))
            .ToListAsync();
        var index = new Dictionary<string, Existing>();
        foreach (var c in existing)
            foreach (var key in Keys(c.LinkedIn, c.Name, c.Phone))
                index.TryAdd(key, c);

        var seenKeys = new Dictionary<string, int>();
        var seenCandidates = new Dictionary<Guid, int>();
        var result = new List<Identified>();

        foreach (var row in rows)
        {
            var name = TextNormalizer.Clean(row.Name);
            var linkedIn = TextNormalizer.NormalizeLinkedIn(row.LinkedIn);
            var phone = TextNormalizer.NormalizePhone(row.Phone);
            var warnings = new List<string>();

            if (name == null)
            {
                result.Add(new(row, null, linkedIn, phone, ImportMatch.Invalid, null, null, [], ["Linha sem nome do profissional."]));
                continue;
            }
            if (phone.Value != null && !phone.Recognized && phone.Value != "inmail") warnings.Add($"Telefone: {phone.Label}.");
            if (name.Contains(" - ")) warnings.Add("O nome contém uma anotação (\" - \"); confira antes de importar.");
            if (row.Notes != null && row.Notes.Length > 4000) warnings.Add("Observação muito longa; será mantida integralmente.");

            var keys = Keys(linkedIn, name, phone.Value);
            var match = keys.Select(k => index.GetValueOrDefault(k)).FirstOrDefault(e => e != null);

            int? duplicateOf = keys.Where(seenKeys.ContainsKey).Select(k => (int?)seenKeys[k]).FirstOrDefault();
            if (duplicateOf == null && match != null && seenCandidates.TryGetValue(match.Id, out var firstRow)) duplicateOf = firstRow;
            foreach (var k in keys) seenKeys.TryAdd(k, row.RowNumber);
            if (match != null) seenCandidates.TryAdd(match.Id, row.RowNumber);

            if (duplicateOf != null)
            {
                result.Add(new(row, name, linkedIn, phone, ImportMatch.DuplicateInFile, match, duplicateOf, [],
                    [.. warnings, $"Mesma pessoa da linha {duplicateOf}."]));
                continue;
            }

            if (match == null)
            {
                result.Add(new(row, name, linkedIn, phone, ImportMatch.New, null, null, [], warnings));
                continue;
            }

            var diffs = Compare(row, linkedIn, phone, match);
            result.Add(new(row, name, linkedIn, phone, diffs.Count > 0 ? ImportMatch.Conflict : ImportMatch.ExactMatch, match, null, diffs, warnings));
        }
        return result;
    }

    private static List<ImportDifference> Compare(ImportRow row, string? linkedIn, TextNormalizer.PhoneResult phone, Existing db)
    {
        var diffs = new List<ImportDifference>();
        void Text(string field, string label, string? file, string? current)
        {
            var f = TextNormalizer.Clean(file);
            var c = TextNormalizer.Clean(current);
            if (f != null && c != null && TextNormalizer.NameKey(f) != TextNormalizer.NameKey(c))
                diffs.Add(new(field, label, f, c));
        }
        Text("currentCompany", "Empresa atual", row.CurrentCompany, db.CurrentCompany);
        Text("currentPosition", "Cargo atual", row.CurrentPosition, db.CurrentPosition);
        if (row.CurrentSalary.HasValue && db.Salary.HasValue && row.CurrentSalary.Value != db.Salary.Value)
            diffs.Add(new("salary", "Remuneração atual", row.CurrentSalary.Value.ToString("0.##"), db.Salary.Value.ToString("0.##")));
        var filePhone = TextNormalizer.PhoneKey(phone.Value);
        var dbPhone = TextNormalizer.PhoneKey(db.Phone);
        if (filePhone != null && dbPhone != null && filePhone != dbPhone)
            diffs.Add(new("phone", "Telefone", phone.Value, db.Phone));
        if (linkedIn != null && db.LinkedIn != null && linkedIn != db.LinkedIn)
            diffs.Add(new("linkedIn", "LinkedIn", linkedIn, db.LinkedIn));
        return diffs;
    }

    private async Task<Dictionary<Guid, Job>> LoadJobs(IEnumerable<Guid> jobIds)
    {
        var ids = jobIds.Distinct().ToList();
        if (ids.Count == 0) throw AppException.BadRequest("Escolha a vaga de destino.");
        var jobs = await db.Jobs.Include(j => j.Position).ThenInclude(p => p.Company)
            .Where(j => ids.Contains(j.Id)).ToDictionaryAsync(j => j.Id);
        if (jobs.Count != ids.Count) throw AppException.NotFound("Vaga");
        var closed = jobs.Values.Where(j => j.Status != JobStatus.Open).Select(j => j.Code).ToList();
        if (closed.Count > 0) throw AppException.BadRequest($"Vaga fechada: {string.Join(", ", closed)}.");
        return jobs;
    }

    public async Task<ImportPreviewResponse> Preview(ImportPreviewRequest req)
    {
        if (req.Rows == null || req.Rows.Count == 0) throw AppException.BadRequest("Nenhuma linha para importar.");
        var jobs = await LoadJobs(req.JobIds ?? []);
        var identified = await Identify(req.Rows);

        var candidateIds = identified.Where(i => i.Existing != null).Select(i => i.Existing!.Id).Distinct().ToList();
        var jobIds = jobs.Keys.ToList();
        var companyIds = jobs.Values.Select(j => j.Position.CompanyId).Distinct().ToList();
        var inJobs = await db.Applications.AsNoTracking()
            .Where(a => candidateIds.Contains(a.CandidateId) && jobIds.Contains(a.JobId))
            .Select(a => new { a.CandidateId, a.JobId }).ToListAsync();
        var offLimits = await db.CandidateOffLimits.AsNoTracking()
            .Where(o => candidateIds.Contains(o.CandidateId) && companyIds.Contains(o.CompanyId))
            .Select(o => new { o.CandidateId, o.CompanyId }).ToListAsync();

        var rows = identified.Select(i => new ImportPreviewRow(
            i.Row.RowNumber, i.Name, i.LinkedIn, i.Phone.Value,
            i.Phone.Value != null && !i.Phone.Recognized && i.Phone.Value != "inmail" ? i.Phone.Label : null,
            i.Match, i.Existing?.Id, i.Existing?.Name, i.DuplicateOfRow, i.Differences,
            i.Existing == null ? [] : inJobs.Where(x => x.CandidateId == i.Existing.Id).Select(x => x.JobId).ToList(),
            i.Existing == null ? [] : offLimits.Where(x => x.CandidateId == i.Existing.Id).Select(x => x.CompanyId).ToList(),
            i.Warnings)).ToList();

        return new ImportPreviewResponse(rows,
            rows.Count(r => r.Match == ImportMatch.New),
            rows.Count(r => r.Match == ImportMatch.ExactMatch),
            rows.Count(r => r.Match == ImportMatch.Conflict),
            rows.Count(r => r.Match == ImportMatch.DuplicateInFile),
            rows.Count(r => r.Match == ImportMatch.Invalid));
    }

    public async Task<ImportConfirmResponse> Confirm(ImportConfirmRequest req, Guid actorUserId)
    {
        if (req.Rows == null || req.Rows.Count == 0) throw AppException.BadRequest("Nenhuma linha para importar.");
        var included = req.Rows.Where(r => r.Include).ToList();
        var jobs = await LoadJobs(included.Count > 0 ? included.Select(r => r.JobId) : req.Rows.Select(r => r.JobId));

        foreach (var r in included)
        {
            if (r.Stage != null && !Stages.IsValid(r.Stage)) throw AppException.BadRequest($"Linha {r.Row.RowNumber}: etapa inválida.");
        }
        var hiresPerJob = included.Where(r => r.Status == ApplicationStatus.Hired).GroupBy(r => r.JobId).Where(g => g.Count() > 1).ToList();
        if (hiresPerJob.Count > 0)
            throw AppException.BadRequest("Considera-se uma contratação por vaga: há mais de uma linha como Contratado para a mesma vaga.");

        var identified = await Identify(req.Rows.Select(r => r.Row));
        var unresolved = req.Rows.Zip(identified)
            .Where(p => p.First.Include && p.Second.Match == ImportMatch.Conflict && p.First.Resolution == null)
            .Select(p => p.First.Row.RowNumber).ToList();
        if (unresolved.Count > 0)
            throw AppException.Conflict($"Resolva os conflitos antes de importar (linhas {string.Join(", ", unresolved)}).", "IMPORT_CONFLICTS");

        var sourceOptions = await db.Options.AsNoTracking().Where(o => o.Category == OptionCategories.Source && o.IsActive)
            .Select(o => o.Value).ToListAsync();
        var existingPairs = (await db.Applications.AsNoTracking()
                .Where(a => jobs.Keys.Contains(a.JobId)).Select(a => new { a.CandidateId, a.JobId }).ToListAsync())
            .Select(x => (x.CandidateId, x.JobId)).ToHashSet();
        var offLimitPairs = (await db.CandidateOffLimits.AsNoTracking().Select(o => new { o.CandidateId, o.CompanyId }).ToListAsync())
            .Select(x => (x.CandidateId, x.CompanyId)).ToHashSet();

        var outcomes = new List<ImportRowOutcome>();
        var batchCandidates = new Dictionary<string, Candidate>();
        var hired = new List<Application>();
        int created = 0, updated = 0, kept = 0, applications = 0;

        await using var tx = await db.Database.BeginTransactionAsync();

        foreach (var (cr, id) in req.Rows.Zip(identified))
        {
            var rowNumber = cr.Row.RowNumber;
            if (id.Match == ImportMatch.Invalid) { outcomes.Add(new(rowNumber, "Skipped", "Linha sem nome do profissional.")); continue; }
            if (!cr.Include)
            {
                var reason = id.Match == ImportMatch.DuplicateInFile
                    ? $"Mesma pessoa da linha {id.DuplicateOfRow} (não incluída)."
                    : "Ignorada pelo usuário.";
                outcomes.Add(new(rowNumber, "Skipped", reason));
                continue;
            }

            // 1) Candidato: existente no banco, já criado neste lote, ou novo.
            var keys = Keys(id.LinkedIn, id.Name, id.Phone.Value);
            Candidate? candidate = keys.Select(k => batchCandidates.GetValueOrDefault(k)).FirstOrDefault(c => c != null);
            if (candidate == null && id.Existing != null)
            {
                candidate = await db.Candidates.FirstAsync(c => c.Id == id.Existing.Id);
                var changed = id.Match == ImportMatch.Conflict && cr.Resolution == ImportResolution.KeepExisting
                    ? false
                    : Merge(candidate, id, overwrite: cr.Resolution == ImportResolution.Update);
                if (changed) { candidate.UpdatedAt = DateTime.UtcNow; updated++; } else kept++;
            }
            else if (candidate == null)
            {
                candidate = new Candidate
                {
                    Name = id.Name!,
                    LinkedIn = id.LinkedIn,
                    Phone = id.Phone.Value,
                    PhoneOriginal = TextNormalizer.Clean(cr.Row.Phone),
                    CurrentCompany = TextNormalizer.Clean(cr.Row.CurrentCompany),
                    CurrentPosition = TextNormalizer.Clean(cr.Row.CurrentPosition),
                    Salary = cr.Row.CurrentSalary,
                };
                db.Candidates.Add(candidate);
                created++;
            }
            foreach (var k in keys) batchCandidates.TryAdd(k, candidate);

            // 2) Prospecção na vaga escolhida para a linha.
            var job = jobs[cr.JobId];
            if (existingPairs.Contains((candidate.Id, job.Id)))
            {
                outcomes.Add(new(rowNumber, "Skipped", $"Candidato já está na vaga {job.Code}."));
                continue;
            }
            var blocked = offLimitPairs.Contains((candidate.Id, job.Position.CompanyId));
            if (blocked && !cr.OffLimitsOverride)
            {
                outcomes.Add(new(rowNumber, "Skipped", $"Off-limits para {job.Position.Company.Name} (sem override)."));
                continue;
            }

            var accessedAt = req.AccessedAt ?? job.OpenedAt;
            var status = cr.Status ?? ApplicationStatus.Active;
            var app = new Application
            {
                Candidate = candidate,
                JobId = job.Id,
                Source = NormalizeSource(cr.Row.Source, sourceOptions),
                CurrentStage = cr.Stage ?? Stages.Acessado,
                RequestedSalary = cr.Row.RequestedSalary,
                AccessedAt = accessedAt,
                Notes = TextNormalizer.Clean(cr.Row.Notes),
                OffLimitsOverride = blocked,
                OffLimitsOverrideByUserId = blocked ? actorUserId : null,
                OffLimitsOverrideAt = blocked ? DateTime.UtcNow : null,
            };
            app.History.Add(new ApplicationHistory
            {
                Stage = app.CurrentStage,
                EnteredAt = PipelineService.EnteredAtFor(accessedAt),
                ActorUserId = actorUserId,
                Notes = $"Importado da planilha (linha {rowNumber}).",
            });
            db.Applications.Add(app);
            existingPairs.Add((candidate.Id, job.Id));
            if (status != ApplicationStatus.Active) pipeline.Close(app, status, null, null, actorUserId);
            if (status == ApplicationStatus.Hired) hired.Add(app);
            applications++;
            outcomes.Add(new(rowNumber, "Imported", null));
        }

        await db.SaveChangesAsync();
        foreach (var app in hired)
            await pipeline.CloseJobAfterHire(app.JobId, app.Id, actorUserId);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return new ImportConfirmResponse(created, updated, kept, applications,
            outcomes.Count(o => o.Outcome == "Skipped"), outcomes);
    }

    /// <summary>Overwrite = "Atualizar" (planilha prevalece). Sem overwrite, só preenche campos vazios no banco.</summary>
    private static bool Merge(Candidate c, Identified id, bool overwrite)
    {
        var changed = false;
        void Set<T>(T? file, T? current, Action<T> apply)
        {
            if (file == null || (file is string s && s.Length == 0)) return;
            if (current == null || overwrite && !Equals(file, current)) { apply(file); changed = true; }
        }
        Set(TextNormalizer.Clean(id.Row.CurrentCompany), c.CurrentCompany, v => c.CurrentCompany = v);
        Set(TextNormalizer.Clean(id.Row.CurrentPosition), c.CurrentPosition, v => c.CurrentPosition = v);
        Set(id.Row.CurrentSalary, c.Salary, v => c.Salary = v);
        Set(id.LinkedIn, c.LinkedIn, v => c.LinkedIn = v);
        if (id.Phone.Value != null && id.Phone.Value != "inmail")
            Set(id.Phone.Value, c.Phone == "inmail" ? null : c.Phone, v => { c.Phone = v; c.PhoneOriginal = TextNormalizer.Clean(id.Row.Phone); });
        else if (c.Phone == null && id.Phone.Value == "inmail") { c.Phone = "inmail"; changed = true; }
        return changed;
    }

    /// <summary>Casa a fonte da planilha com o vocabulário ("hunting" → "Hunting", "Indicações" → "Indicação").</summary>
    public static string? NormalizeSource(string? raw, IEnumerable<string> options)
    {
        var clean = TextNormalizer.Clean(raw);
        if (clean == null) return null;
        static string Key(string s)
        {
            var k = TextNormalizer.RemoveDiacritics(s).Trim().ToLowerInvariant();
            if (k.EndsWith("oes")) return k[..^3] + "ao";
            return k.EndsWith('s') ? k[..^1] : k;
        }
        var key = Key(clean);
        return options.FirstOrDefault(o => Key(o) == key) ?? clean;
    }
}
