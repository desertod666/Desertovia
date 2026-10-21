using Desertod.Domain.Director;
using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Infrastructure.Data;

public sealed partial class DesertodDbContext
{
    public Guid? DirectorReviewId { get; set; }
    public DbSet<DirectorFinancePolicy> DirectorFinancePolicies => Set<DirectorFinancePolicy>();
    public DbSet<DirectorFinanceDay> DirectorFinanceDays => Set<DirectorFinanceDay>();
    public DbSet<DirectorFinanceAction> DirectorFinanceActions => Set<DirectorFinanceAction>();

    private static void ConfigureDirectorFinance(ModelBuilder model)
    {
        model.Entity<DirectorFinancePolicy>(b =>
        {
            b.HasKey(x => new { x.VillageId, x.Version });
            b.HasIndex(x => new { x.VillageId, x.RequestId }).IsUnique();
            b.Property(x => x.Fingerprint).HasMaxLength(64);
            b.OwnsOne(x => x.Limits, p => p.ToJson());
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<DirectorFinanceDay>(b =>
        {
            b.HasKey(x => new { x.VillageId, x.Day }); b.Property(x => x.Revision).IsConcurrencyToken();
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<AlmightyObservation>().HasAlternateKey(x => new { x.VillageId, x.Id });
        model.Entity<DirectorFinanceAction>(b =>
        {
            b.HasKey(x => new { x.VillageId, x.ReviewId }); b.HasIndex(x => new { x.VillageId, x.At });
            b.Property(x => x.Terms).HasColumnType("jsonb"); b.Property(x => x.Before).HasColumnType("jsonb");
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<DirectorFinancePolicy>().WithMany().HasForeignKey(x => new { x.VillageId, x.PolicyVersion }).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<DirectorFinanceDay>().WithMany().HasForeignKey(x => new { x.VillageId, x.Day }).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<AlmightyObservation>().WithMany().HasForeignKey(x => new { x.VillageId, Id = x.ReviewId })
                .HasPrincipalKey(x => new { x.VillageId, x.Id }).OnDelete(DeleteBehavior.NoAction);
        });
        model.Entity<LedgerEntry>().HasOne<DirectorFinanceAction>().WithMany()
            .HasForeignKey(x => new { x.VillageId, ReviewId = x.DirectorReviewId }).OnDelete(DeleteBehavior.NoAction);
    }
}
