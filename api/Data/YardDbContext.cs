using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Portfolio.Yard.Api.Data;

public sealed class YardDbContext(DbContextOptions<YardDbContext> options) : DbContext(options)
{
    public DbSet<YardSpot> Spots => Set<YardSpot>();
    public DbSet<Trailer> Trailers => Set<Trailer>();
    public DbSet<GateEventRecord> GateEvents => Set<GateEventRecord>();
    public DbSet<TrailerMove> Moves => Set<TrailerMove>();
    public DbSet<Inspection> Inspections => Set<Inspection>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<YardSpot>(e =>
        {
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(10);
            e.Property(x => x.Kind).HasMaxLength(10);
        });

        model.Entity<Trailer>(e =>
        {
            e.HasKey(x => x.TrailerNumber);
            e.Property(x => x.TrailerNumber).HasMaxLength(Limits.Trailer);
            e.Property(x => x.Equipment).HasMaxLength(20);
            e.Property(x => x.Status).HasMaxLength(20);
            e.Property(x => x.SpotCode).HasMaxLength(10);
            e.Property(x => x.CurrentLoadNumber).HasMaxLength(30);
            e.Property(x => x.Carrier).HasMaxLength(120);
            e.Property(x => x.HoldReason).HasMaxLength(Limits.Note);
            e.HasIndex(x => x.Status);
            // A spot holds at most one trailer; the database enforces it.
            e.HasIndex(x => x.SpotCode).IsUnique();
            e.HasOne<YardSpot>().WithMany().HasForeignKey(x => x.SpotCode).OnDelete(DeleteBehavior.Restrict);
        });

        model.Entity<GateEventRecord>(e =>
        {
            e.ToTable("GateEvents");
            e.Property(x => x.TrailerNumber).HasMaxLength(Limits.Trailer);
            e.Property(x => x.Direction).HasMaxLength(3);
            e.Property(x => x.Note).HasMaxLength(Limits.Note);
            e.HasIndex(x => new { x.TrailerNumber, x.OccurredAt });
            e.HasIndex(x => x.OccurredAt);
        });

        model.Entity<TrailerMove>(e =>
        {
            e.Property(x => x.TrailerNumber).HasMaxLength(Limits.Trailer);
            e.Property(x => x.FromSpot).HasMaxLength(10);
            e.Property(x => x.ToSpot).HasMaxLength(10);
            e.HasIndex(x => new { x.TrailerNumber, x.OccurredAt });
        });

        model.Entity<Inspection>(e =>
        {
            e.Property(x => x.TrailerNumber).HasMaxLength(Limits.Trailer);
            e.Property(x => x.Notes).HasMaxLength(Limits.Note);
            e.HasIndex(x => new { x.TrailerNumber, x.OccurredAt });
            e.HasIndex(x => x.OccurredAt);
        });

        model.Entity<OutboxMessage>(e =>
        {
            e.ToTable("Outbox");
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventType).HasMaxLength(60);
            e.Property(x => x.TrailerNumber).HasMaxLength(Limits.Trailer);
            e.Property(x => x.Details).HasMaxLength(Limits.Note);
            e.Property(x => x.LastError).HasMaxLength(Limits.Note);
            e.HasIndex(x => new { x.SentAt, x.NextAttemptAt });
        });
    }
}

/// <summary>Used by `dotnet ef migrations add`; migrations target SQL Server.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<YardDbContext>
{
    public YardDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<YardDbContext>()
            .UseSqlServer("Server=localhost;Database=yard;User Id=sa;Password=design-time;TrustServerCertificate=True")
            .Options);
}
