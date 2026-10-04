using Microsoft.EntityFrameworkCore;
using RadarTalentos.API.Entities;

namespace RadarTalentos.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Candidate> Candidates => Set<Candidate>();
    public DbSet<CandidateOffLimit> CandidateOffLimits => Set<CandidateOffLimit>();
    public DbSet<Application> Applications => Set<Application>();
    public DbSet<ApplicationHistory> ApplicationHistory => Set<ApplicationHistory>();
    public DbSet<OptionItem> Options => Set<OptionItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Soft delete (D8): todas as relações usam Restrict — nada é apagado em cascata.
        b.Entity<User>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
        });

        b.Entity<Company>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.NormalizedName).IsUnique();
        });

        b.Entity<Position>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.NormalizedName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Modality).HasMaxLength(30);
            e.HasIndex(x => new { x.CompanyId, x.NormalizedName }).IsUnique();
            e.HasOne(x => x.Company).WithMany(c => c.Positions).HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Job>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(60).IsRequired();
            e.Property(x => x.Level).HasMaxLength(60);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.PositionId, x.Sequence }).IsUnique();
            e.HasOne(x => x.Position).WithMany(p => p.Jobs).HasForeignKey(x => x.PositionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.ReopenedFrom).WithMany().HasForeignKey(x => x.ReopenedFromJobId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Candidate>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.State).HasMaxLength(2);
            e.Property(x => x.LinkedIn).HasMaxLength(300);
            e.Property(x => x.Salary).HasPrecision(12, 2);
            e.HasIndex(x => x.LinkedIn).IsUnique().HasFilter("\"LinkedIn\" IS NOT NULL AND \"IsActive\"");
            e.HasIndex(x => x.Name);
        });

        b.Entity<CandidateOffLimit>(e =>
        {
            e.HasKey(x => new { x.CandidateId, x.CompanyId });
            e.HasOne(x => x.Candidate).WithMany(c => c.OffLimits).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Company).WithMany().HasForeignKey(x => x.CompanyId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Application>(e =>
        {
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CurrentStage).HasMaxLength(30).IsRequired();
            e.Property(x => x.Source).HasMaxLength(60);
            e.Property(x => x.OfferedSalary).HasPrecision(12, 2);
            e.Property(x => x.RequestedSalary).HasPrecision(12, 2);
            e.HasIndex(x => new { x.CandidateId, x.JobId }).IsUnique();
            e.HasIndex(x => x.Status);
            e.HasOne(x => x.Candidate).WithMany(c => c.Applications).HasForeignKey(x => x.CandidateId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Job).WithMany(j => j.Applications).HasForeignKey(x => x.JobId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<ApplicationHistory>(e =>
        {
            e.Property(x => x.Stage).HasMaxLength(30).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.ApplicationId);
            e.HasOne(x => x.Application).WithMany(a => a.History).HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<OptionItem>(e =>
        {
            e.Property(x => x.Category).HasMaxLength(30).IsRequired();
            e.Property(x => x.Value).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.Category, x.Value }).IsUnique();
        });
    }
}
