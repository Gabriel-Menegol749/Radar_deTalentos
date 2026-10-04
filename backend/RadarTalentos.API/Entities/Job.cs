namespace RadarTalentos.API.Entities;

public enum JobStatus { Open, Closed }

public class Job
{
    public Guid Id { get; set; }
    public Guid PositionId { get; set; }
    public Position Position { get; set; } = null!;
    public string Code { get; set; } = "";
    /// <summary>Número da abertura dentro da posição (o "NN" do código).</summary>
    public int Sequence { get; set; }
    /// <summary>Nível da vaga quando um processo cobre vários níveis (D2). Opcional.</summary>
    public string? Level { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Open;
    public DateOnly OpenedAt { get; set; }
    public DateOnly? ClosedAt { get; set; }
    public Guid? OwnerUserId { get; set; }
    public User? Owner { get; set; }
    public Guid? ReopenedFromJobId { get; set; }
    public Job? ReopenedFrom { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Application> Applications { get; set; } = [];
}
