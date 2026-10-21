using Microsoft.EntityFrameworkCore;

namespace PricingExceptions.Api;

public sealed class PricingDb(DbContextOptions<PricingDb> options) : DbContext(options)
{
    public DbSet<PricingRequest> Requests => Set<PricingRequest>();
    public DbSet<RequestRevision> Revisions => Set<RequestRevision>();
    public DbSet<RequestDecision> Decisions => Set<RequestDecision>();
    public DbSet<MortgageApplication> Applications => Set<MortgageApplication>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        var request = model.Entity<PricingRequest>();
        request.HasKey(x => x.Id);
        request.Property(x => x.Status).HasConversion<string>();
        request.Property(x => x.Version).IsConcurrencyToken();
        request.Property(x => x.CreatedAt).HasConversion(
            value => value.UtcDateTime,
            value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));
        request.HasIndex(x => x.ApplicationId).IsUnique()
            .HasFilter("\"Status\" IN ('Pending', 'Approved')");
        request.HasOne<MortgageApplication>().WithMany().HasForeignKey(x => x.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        request.HasMany(x => x.Revisions).WithOne().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        request.HasMany(x => x.Decisions).WithOne().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<RequestRevision>().HasKey(x => new { x.RequestId, x.Number });
        model.Entity<RequestDecision>().HasKey(x => x.Id);
        model.Entity<RequestDecision>().HasIndex(x => x.RequestId).IsUnique();
        model.Entity<RequestDecision>().Property(x => x.Outcome).HasConversion<string>();
        model.Entity<RequestDecision>().HasOne<RequestRevision>().WithMany()
            .HasForeignKey(x => new { x.RequestId, Number = x.RevisionNumber }).OnDelete(DeleteBehavior.Restrict);
        model.Entity<MortgageApplication>().HasKey(x => x.Id);
        model.Entity<IdempotencyRecord>().HasKey(x => new { x.Actor, x.Operation, x.Key });
        model.Entity<MortgageApplication>().HasData(
            new MortgageApplication("app-1001", "Purchase ? fixed-rate mortgage"),
            new MortgageApplication("app-1002", "Remortgage ? fixed-rate mortgage"),
            new MortgageApplication("app-1003", "Purchase ? joint application"));
    }
}

public sealed record MortgageApplication(string Id, string Description);

public sealed class IdempotencyRecord
{
    public string Actor { get; set; } = "";
    public string Operation { get; set; } = "";
    public string Key { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public string Location { get; set; } = "";
    public string ResponseJson { get; set; } = "";
}
