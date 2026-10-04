using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Data;
using RadarTalentos.API.DTOs;
using RadarTalentos.API.Entities;
using RadarTalentos.API.Infrastructure;

namespace RadarTalentos.API.Services;

public class JobService(AppDbContext db)
{
    /// <summary>D10: EMPRESA-POSICAO-AAMMDD-NN (empresa até 12, posição até 14 caracteres).</summary>
    public static string BuildCode(string companyName, string positionName, DateOnly openedAt, int sequence) =>
        $"{TextNormalizer.SlugPart(companyName, 12)}-{TextNormalizer.SlugPart(positionName, 14)}-{openedAt:yyMMdd}-{sequence:D2}";

    public IQueryable<Job> Query() =>
        db.Jobs.Include(j => j.Position).ThenInclude(p => p.Company).Include(j => j.Owner).Include(j => j.ReopenedFrom);

    public static JobDto ToDto(Job j, int applicationsCount)
    {
        var end = j.Status == JobStatus.Closed && j.ClosedAt.HasValue ? j.ClosedAt.Value : DateOnly.FromDateTime(DateTime.Today);
        return new JobDto(
            j.Id, j.Code, j.PositionId, j.Position.Name, j.Position.CompanyId, j.Position.Company.Name, j.Level,
            j.Status, j.OpenedAt, j.ClosedAt, end.DayNumber - j.OpenedAt.DayNumber, j.OwnerUserId, j.Owner?.Name,
            j.Position.Modality, j.Position.TravelAvailability, j.Position.Requirements, applicationsCount, j.ReopenedFrom?.Code);
    }

    public async Task<string> PreviewCode(Guid? positionId, Guid? companyId, string? positionName, DateOnly openedAt)
    {
        Position? position = null;
        string companyName, name;
        if (positionId.HasValue)
        {
            position = await db.Positions.Include(p => p.Company).FirstOrDefaultAsync(p => p.Id == positionId)
                ?? throw AppException.NotFound("Posição");
            companyName = position.Company.Name;
            name = position.Name;
        }
        else
        {
            var company = await db.Companies.FindAsync(companyId) ?? throw AppException.NotFound("Empresa");
            name = TextNormalizer.Clean(positionName) ?? throw AppException.BadRequest("Informe o nome da posição.");
            companyName = company.Name;
            var normalized = TextNormalizer.NormalizeName(name);
            position = await db.Positions.FirstOrDefaultAsync(p => p.CompanyId == company.Id && p.NormalizedName == normalized);
        }
        var sequence = position == null ? 1 : await NextSequence(position.Id);
        return BuildCode(companyName, name, openedAt, sequence);
    }

    public async Task<Position> GetOrCreatePosition(Guid companyId, string name, string? requirements, string? modality, bool? travel)
    {
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId && c.IsActive) ?? throw AppException.NotFound("Empresa");
        var clean = TextNormalizer.Clean(name) ?? throw AppException.BadRequest("Informe o nome da posição.");
        var normalized = TextNormalizer.NormalizeName(clean);
        var position = await db.Positions.Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.NormalizedName == normalized);
        if (position == null)
        {
            position = new Position { CompanyId = companyId, Company = company, Name = clean, NormalizedName = normalized };
            db.Positions.Add(position);
        }
        // Como no protótipo: reutilizar a posição atualiza requisitos/modalidade/viagem quando informados.
        if (requirements != null) position.Requirements = TextNormalizer.Clean(requirements);
        if (modality != null) position.Modality = ValidateModality(modality);
        if (travel.HasValue) position.TravelAvailability = travel.Value;
        return position;
    }

    public static string ValidateModality(string modality) =>
        Modalities.All.Contains(modality) ? modality : throw AppException.BadRequest("Modalidade inválida.");

    private async Task<int> NextSequence(Guid positionId) =>
        (await db.Jobs.Where(j => j.PositionId == positionId).MaxAsync(j => (int?)j.Sequence) ?? 0) + 1;

    /// <summary>Cria a vaga gerando o código no servidor. O índice único resolve corridas: em conflito, tenta o próximo número.</summary>
    public async Task<Job> CreateJob(Position position, DateOnly openedAt, string? level, Guid? ownerUserId, Guid? reopenedFromJobId)
    {
        if (ownerUserId.HasValue && !await db.Users.AnyAsync(u => u.Id == ownerUserId && u.IsActive))
            throw AppException.BadRequest("Responsável inválido.");

        var isNewPosition = db.Entry(position).State == EntityState.Added;
        var sequence = isNewPosition ? 1 : await NextSequence(position.Id);
        for (var attempt = 0; ; attempt++)
        {
            var job = new Job
            {
                Position = position,
                Sequence = sequence,
                Code = BuildCode(position.Company.Name, position.Name, openedAt, sequence),
                Level = TextNormalizer.Clean(level),
                OpenedAt = openedAt,
                OwnerUserId = ownerUserId,
                ReopenedFromJobId = reopenedFromJobId,
            };
            db.Jobs.Add(job);
            try
            {
                await db.SaveChangesAsync();
                return job;
            }
            catch (DbUpdateException ex) when (ExceptionMiddleware.IsUniqueViolation(ex) && attempt < 5)
            {
                db.Entry(job).State = EntityState.Detached;
                sequence++;
            }
        }
    }

    public async Task<Job> Close(Guid id)
    {
        var job = await db.Jobs.FindAsync(id) ?? throw AppException.NotFound("Vaga");
        if (job.Status == JobStatus.Closed) throw AppException.BadRequest("A vaga já está fechada.");
        job.Status = JobStatus.Closed;
        job.ClosedAt = DateOnly.FromDateTime(DateTime.Today);
        await db.SaveChangesAsync();
        return job;
    }

    /// <summary>Como no protótipo: reabrir cria uma nova abertura (novo código) para a mesma posição.</summary>
    public async Task<Job> Reopen(Guid id, Guid actorUserId)
    {
        var original = await db.Jobs.Include(j => j.Position).ThenInclude(p => p.Company).FirstOrDefaultAsync(j => j.Id == id)
            ?? throw AppException.NotFound("Vaga");
        if (original.Status != JobStatus.Closed) throw AppException.BadRequest("Só é possível reabrir uma vaga fechada.");
        return await CreateJob(original.Position, DateOnly.FromDateTime(DateTime.Today), original.Level,
            original.OwnerUserId ?? actorUserId, original.Id);
    }
}
