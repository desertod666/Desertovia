using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Infrastructure.Data;

public sealed partial class DesertodDbContext
{
    public DbSet<TreasuryFundingPolicy> TreasuryFundingPolicies => Set<TreasuryFundingPolicy>();
    public DbSet<TreasuryIssueDay> TreasuryIssueDays => Set<TreasuryIssueDay>();
    public DbSet<TreasuryIssue> TreasuryIssues => Set<TreasuryIssue>();

    private static void ConfigureTreasuryFunding(ModelBuilder model)
    {
        model.Entity<TreasuryFundingPolicy>(b =>
        {
            b.HasKey(x => new { x.VillageId, x.Version }); b.HasIndex(x => new { x.VillageId, x.RequestId }).IsUnique();
            b.HasIndex(x => new { x.VillageId, x.EffectiveAt, x.Version }); b.Property(x => x.RequestFingerprint).HasMaxLength(64);
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<TreasuryIssueDay>(b =>
        {
            b.HasKey(x => new { x.VillageId, x.Day }); b.Property(x => x.Revision).IsConcurrencyToken();
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<TreasuryIssue>(b =>
        {
            b.HasKey(x => new { x.VillageId, x.RequestId }); b.HasIndex(x => new { x.VillageId, x.At });
            b.Property(x => x.RequestFingerprint).HasMaxLength(64);
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<TreasuryFundingPolicy>().WithMany().HasForeignKey(x => new { x.VillageId, x.PolicyVersion }).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<TreasuryIssueDay>().WithMany().HasForeignKey(x => new { x.VillageId, x.Day }).OnDelete(DeleteBehavior.NoAction);
            b.HasOne<MonetaryBatch>().WithMany().HasForeignKey(x => new { x.VillageId, x.FinancialBatchId }).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
