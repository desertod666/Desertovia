using Desertod.Domain.Economy;
using Desertod.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Desertod.Infrastructure.Data;

public sealed partial class DesertodDbContext
{
    // Transaction-local context, initialized under the existing world advisory lock.
    public Guid? FinancialBatchId { get; set; }
    public bool FinancialBatchStarted { get; set; }
    public DbSet<MonetaryBatch> MonetaryBatches => Set<MonetaryBatch>();

    private static void ConfigureMonetaryAudit(ModelBuilder model)
    {
        model.Entity<MonetaryBatch>(b =>
        {
            b.HasKey(x => new { x.VillageId, x.Id }); b.HasIndex(x => new { x.VillageId, x.At });
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
            foreach (var name in new[] { "BeforeTotal", "AfterTotal", "Issued", "Withdrawn", "Transferred" })
                b.Property<decimal>(name).HasPrecision(28, 0);
            b.Property(x => x.BeforeHash).HasMaxLength(64); b.Property(x => x.AfterHash).HasMaxLength(64);
            b.Property(x => x.PostingHash).HasMaxLength(64);
            b.OwnsMany(x => x.Operations, o => o.ToJson());
        });
        model.Entity<LedgerEntry>().HasOne<MonetaryBatch>().WithMany()
            .HasForeignKey(x => new { x.VillageId, x.FinancialBatchId }).OnDelete(DeleteBehavior.NoAction);
    }
}
