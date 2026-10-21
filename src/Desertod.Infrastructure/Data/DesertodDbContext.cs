using Microsoft.EntityFrameworkCore.ChangeTracking;
using Desertod.Domain.Economy;
using Desertod.Domain.Notifications;
using Desertod.Domain.Farming;
using Desertod.Domain.Director;
using Desertod.Domain.Chat;
using Desertod.Domain.Dungeons;
using Desertod.Domain.Entities;
using Desertod.Domain.Projects;
using Desertod.Domain.Quests;
using Desertod.Domain.World;
using Desertod.Domain.Combat;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Desertod.Infrastructure.Data;

public sealed class DesertodDbContext(DbContextOptions<DesertodDbContext> options) : DbContext(options)
{
    public int SuccessfulSaves { get; private set; }

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<Village>().Where(e => e.State != EntityState.Deleted).ToArray())
        {
            FortificationUpgrades.ApplyReady(entry.Entity);
            Desertod.Domain.Buildings.BuildingNumbers.Ensure(entry.Entity);
        }
        var count = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        SuccessfulSaves++;
        return count;
    }

    public Guid? AuditActor { get; set; }
    public string? AuditOperation { get; set; }

    public DbSet<PlayerReport> PlayerReports => Set<PlayerReport>();

    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();
    public DbSet<PasswordLogin> PasswordLogins => Set<PasswordLogin>();
    public DbSet<Village> Villages => Set<Village>();
    public DbSet<Citizen> Citizens => Set<Citizen>();
    public DbSet<VillageProject> VillageProjects => Set<VillageProject>();
    public DbSet<VillageQuest> VillageQuests => Set<VillageQuest>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<ChaplainMessage> ChaplainMessages => Set<ChaplainMessage>();
    public DbSet<DungeonRun> DungeonRuns => Set<DungeonRun>();

    public DbSet<AlmightyState> AlmightyStates => Set<AlmightyState>();
    public DbSet<AlmightyProposal> AlmightyProposals => Set<AlmightyProposal>();
    public DbSet<AlmightyObservation> AlmightyObservations => Set<AlmightyObservation>();

    public DbSet<KingdomEconomy> KingdomEconomies => Set<KingdomEconomy>();
    public DbSet<GameWallet> GameWallets => Set<GameWallet>();
    public DbSet<EconomyReceipt> EconomyReceipts => Set<EconomyReceipt>();
    public DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();
    public DbSet<MarketLot> MarketLots => Set<MarketLot>();
    public DbSet<SupplyOrder> SupplyOrders => Set<SupplyOrder>();
    public DbSet<SupplyDelivery> SupplyDeliveries => Set<SupplyDelivery>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<NotificationPreferenceReceipt> NotificationPreferenceReceipts => Set<NotificationPreferenceReceipt>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<WorkOrderClaim> WorkOrderClaims => Set<WorkOrderClaim>();
    public DbSet<FarmCropRecord> FarmCrops => Set<FarmCropRecord>();

    public DbSet<RaidLaunchReceipt> RaidLaunchReceipts => Set<RaidLaunchReceipt>();
    public DbSet<Desertod.Domain.Combat.RaidBattle> RaidBattles => Set<Desertod.Domain.Combat.RaidBattle>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<NotificationPreference>(b=>{
            b.HasKey(x=>x.AccountId);b.Property(x=>x.Revision).IsConcurrencyToken();b.Property(x=>x.TimeZoneId).HasMaxLength(64);
            b.HasOne<Account>().WithOne().HasForeignKey<NotificationPreference>(x=>x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<NotificationPreferenceReceipt>(b=>{
            b.HasKey(x=>new{x.AccountId,x.RequestId});b.HasIndex(x=>new{x.AccountId,x.At});b.Property(x=>x.Fingerprint).HasMaxLength(64);
            b.HasOne<Account>().WithMany().HasForeignKey(x=>x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<PlayerReport>(b =>
        {
            b.HasKey(r => r.Id);
            b.Property(r => r.Kind).HasMaxLength(16);
            b.Property(r => r.Text).HasMaxLength(2000);
            b.Property(r => r.Fingerprint).HasMaxLength(64);
            b.Property(r => r.DeliveryState).HasMaxLength(16);
            b.HasIndex(r => new { r.AccountId, r.CreatedAt });
            b.HasIndex(r => new { r.DeliveryState, r.NextAttemptAt });
            b.HasOne<Account>().WithMany().HasForeignKey(r => r.AccountId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne<Village>().WithMany().HasForeignKey(r => r.VillageId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<RaidLaunchReceipt>(receipt=>
        {
            receipt.HasKey(r=>new{r.VillageId,r.Id});
            receipt.HasOne<Village>().WithMany().HasForeignKey(r=>r.VillageId).OnDelete(DeleteBehavior.Cascade);
            receipt.OwnsOne(r=>r.Settings,settings=>{settings.ToJson();MapRaidSettings(settings);});
        });
        modelBuilder.Entity<Desertod.Domain.Combat.RaidBattle>(battle =>
        {
            battle.HasKey(b=>b.Id);
            battle.HasOne<Desertod.Domain.Entities.Village>().WithMany().HasForeignKey(b=>b.VillageId).OnDelete(DeleteBehavior.Cascade);
            battle.HasIndex(b=>new { b.VillageId, b.FinishedAt });
            battle.OwnsMany(b=>b.Units, unit=> { unit.ToJson(); unit.Ignore(u=>u.Position); unit.Ignore(u=>u.Alive); unit.Property(u=>u.Enemy).HasConversion<string>(); });
            battle.OwnsOne(b=>b.Settings, settings=>{settings.ToJson();MapRaidSettings(settings);});
            battle.OwnsMany(b=>b.Fighters, fighter=> { fighter.ToJson(); fighter.Ignore(f=>f.Position); fighter.Ignore(f=>f.ActorId); fighter.Property(f=>f.Weapon).HasConversion<string>(); fighter.Property(f=>f.Armor).HasConversion<string>(); });
            battle.OwnsMany(b=>b.LootBudgets,loot=>{loot.ToJson();loot.Property(l=>l.Kind).HasConversion<string>();});
            battle.OwnsMany(b=>b.Damage, damage=>damage.ToJson());
            battle.OwnsMany(b=>b.Events, events=>events.ToJson());
        });
        modelBuilder.Entity<Village>().Property(v=>v.VigorEnergy).HasDefaultValue(8);
        modelBuilder.Entity<Village>().Property(v=>v.TsarRestState).HasConversion<string>().HasMaxLength(16).HasDefaultValue(Desertod.Domain.Rules.RestState.Active);
        modelBuilder.Entity<Village>().Property(v=>v.VigorCooldownMinutes).HasDefaultValue(240);
        modelBuilder.Entity<KingdomEconomy>(b => {
            b.HasKey(x=>x.VillageId); b.HasOne<Village>().WithMany().HasForeignKey(x=>x.VillageId).OnDelete(DeleteBehavior.Cascade);
            b.OwnsMany(x=>x.TradeReserves,r=>{r.ToJson();r.Property(x=>x.Resource).HasConversion<string>();});
        });
        modelBuilder.Entity<SupplyOrder>(b => {
            b.HasKey(x=>x.Id);b.HasAlternateKey(x=>new{x.VillageId,x.Id});b.HasIndex(x=>new{x.VillageId,x.Status,x.ExpiresAt});
            b.Property(x=>x.Resource).HasConversion<string>();b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasOne<Village>().WithMany().HasForeignKey(x=>x.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<SupplyDelivery>(b => {
            b.HasKey(x=>x.Id);b.HasIndex(x=>new{x.VillageId,x.SupplierAccountId,x.At});
            b.Property(x=>x.Resource).HasConversion<string>();
            b.HasOne<SupplyOrder>().WithMany().HasForeignKey(x=>new{x.VillageId,x.SupplyOrderId})
                .HasPrincipalKey(x=>new{x.VillageId,x.Id}).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<WorkOrder>(b => {
            b.HasKey(x=>x.Id); b.HasAlternateKey(x=>new{x.VillageId,x.Id});
            b.HasIndex(x=>new{x.VillageId,x.Status,x.ExpiresAt});
            b.Property(x=>x.TaskId).HasMaxLength(80); b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasOne<Village>().WithMany().HasForeignKey(x=>x.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<WorkOrderClaim>(b => {
            b.HasKey(x=>x.Id); b.HasIndex(x=>new{x.CitizenId,x.CycleStartedAt}).IsUnique();
            b.HasIndex(x=>new{x.VillageId,x.AccountId,x.Status});
            b.Property(x=>x.Status).HasConversion<string>().HasMaxLength(16);
            b.HasOne<WorkOrder>().WithMany().HasForeignKey(x=>new{x.VillageId,x.WorkOrderId}).HasPrincipalKey(x=>new{x.VillageId,x.Id}).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<GameWallet>(b => { b.HasKey(x=>new{x.VillageId,x.AccountId}); b.HasOne<Village>().WithMany().HasForeignKey(x=>x.VillageId).OnDelete(DeleteBehavior.Cascade); b.HasOne<Account>().WithMany().HasForeignKey(x=>x.AccountId); });
        modelBuilder.Entity<FarmCropRecord>(b=>
        {
            b.HasKey(c=>c.Id);b.HasIndex(c=>new{c.VillageId,c.PlotId,c.Id});
            b.Property(c=>c.PlotId).HasMaxLength(40);b.Property(c=>c.Crop).HasConversion<string>();
            b.OwnsMany(c=>c.Contributions,a=>{a.ToJson();a.Property(x=>x.Step).HasConversion<string>();a.Property(x=>x.Name).HasMaxLength(80);});
            b.OwnsMany(c=>c.Produced,g=>{g.ToJson();g.Property(x=>x.Resource).HasConversion<string>();});
            b.OwnsMany(c=>c.Lost,g=>{g.ToJson();g.Property(x=>x.Resource).HasConversion<string>();});
            b.HasOne<Village>().WithMany().HasForeignKey(c=>c.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<EconomyReceipt>(b => { b.HasKey(x=>new{x.VillageId,x.RequestId}); b.Property(x=>x.Fingerprint).HasMaxLength(128); b.HasOne<Village>().WithMany().HasForeignKey(x=>x.VillageId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<LedgerEntry>(b => { b.HasKey(x=>x.Id); b.HasIndex(x=>new{x.VillageId,x.At,x.Id}); b.HasIndex(x=>new{x.VillageId,x.OperationId}); b.Property(x=>x.Operation).HasMaxLength(80); b.Property(x=>x.Asset).HasMaxLength(40); b.Property(x=>x.Location).HasMaxLength(100); b.HasOne<Village>().WithMany().HasForeignKey(x=>x.VillageId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<MarketLot>(b => { b.HasKey(x=>x.Id); b.HasIndex(x=>new{x.VillageId,x.Status,x.CreatedAt}); b.Property(x=>x.Resource).HasConversion<string>(); b.Property(x=>x.Status).HasMaxLength(16); b.HasOne<Village>().WithMany().HasForeignKey(x=>x.VillageId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<AlmightyState>(b => {
            b.HasKey(x => x.VillageId);
            b.Property(x => x.Version).IsConcurrencyToken();
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.NextReviewAt);
        });
        modelBuilder.Entity<AlmightyProposal>(b => {
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.ReviewId).IsUnique();
            b.HasIndex(x => new { x.VillageId, x.Status, x.CreatedAt });
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<AlmightyObservation>(b => {
            b.HasKey(x => x.Id);
            b.HasIndex(x => new { x.VillageId, x.At });
            b.HasOne<Village>().WithMany().HasForeignKey(x => x.VillageId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<Account>(account =>
        {
            account.HasKey(a => a.Id);
            account.Property(a => a.DisplayName).HasMaxLength(64);
            account.HasMany(a => a.Logins).WithOne().HasForeignKey(l => l.AccountId);
            account.OwnsOne(a => a.Character, character =>
            {
                character.ToJson();
                character.OwnsOne(c => c.Appearance, appearance => appearance.Property(a => a.Gender).HasConversion<string>());
            });
        });

        modelBuilder.Entity<ExternalLogin>(login =>
        {
            login.HasKey(l => new { l.Provider, l.ProviderUserId });
            login.Property(l => l.Provider).HasMaxLength(16);
            login.Property(l => l.ProviderUserId).HasMaxLength(128);
            login.Property(l => l.ProviderUserName).HasMaxLength(128);
        });

        modelBuilder.Entity<PasswordLogin>(login =>
        {
            login.HasKey(l => l.Email);
            login.Property(l => l.Email).HasMaxLength(PasswordLogin.EmailMaximumLength);
            login.Property(l => l.PasswordHash).HasMaxLength(256);
            login.Property(l => l.RegisteredFrom).HasMaxLength(PasswordLogin.AddressMaximumLength);
            login.HasIndex(l => l.AccountId).IsUnique();
            login.HasIndex(l => new { l.RegisteredFrom, l.RegisteredAt });
            login.HasOne<Account>().WithMany().HasForeignKey(l => l.AccountId);
        });

        modelBuilder.Entity<Village>(village =>
        {
            village.HasKey(v => v.Id);
            village.HasIndex(v => v.Slug).IsUnique();
            village.Property(v => v.Slug).HasMaxLength(48);
            village.Property(v => v.Name).HasMaxLength(Village.NameMaximumLength);
            village.Property(v => v.Description).HasMaxLength(Village.DescriptionMaximumLength);
            village.Property(v => v.InviteCode).HasMaxLength(Village.InviteCodeLength);
            village.HasIndex(v => v.IsPrivate);
            village.HasOne<Account>().WithMany().HasForeignKey(v => v.OwnerAccountId);
            village.OwnsMany(v => v.Storages, storage =>
            {
                storage.ToJson();
                storage.Property(s => s.Kind).HasConversion<string>();
                storage.Ignore(s => s.Definition);
                storage.OwnsMany(s => s.PersonalCosts, cost => cost.Property(g => g.Kind).HasConversion<string>());
                storage.OwnsMany(s => s.Goods, good => good.Property(g => g.Kind).HasConversion<string>());
                storage.OwnsMany(s => s.Investment, good => good.Property(g => g.Kind).HasConversion<string>());
            });
            village.OwnsMany(v => v.Structures, structure =>
            {
                structure.ToJson();
                structure.Property(s => s.Kind).HasConversion<string>();
                structure.OwnsMany(s => s.Investment, good => good.Property(g => g.Kind).HasConversion<string>());
            });
            village.OwnsMany(v=>v.CivicOrders, order=>
            {
                order.ToJson();
                order.Property(o=>o.BuildingKind).HasConversion<string>();
            });
            village.OwnsMany(v => v.Sites, site =>
            {
                site.ToJson();
                site.Property(s => s.Kind).HasConversion<string>();
                site.Ignore(s => s.Definition);
                site.Ignore(s => s.IsFinished);
                site.Ignore(s => s.HasWork);
                site.OwnsMany(s => s.Progress);
                site.OwnsMany(s => s.Hands);
                site.OwnsMany(s => s.Salvage, good => good.Property(g=>g.Kind).HasConversion<string>());
            });
            village.OwnsMany(v=>v.SiteRecoveries, recovery=>
            {
                recovery.ToJson();
                recovery.OwnsMany(r=>r.Goods, good=>good.Property(g=>g.Kind).HasConversion<string>());
            });
            village.OwnsMany(v => v.Wildlife, herd =>
            {
                herd.ToJson();
                herd.Property(h => h.Kind).HasConversion<string>();
                herd.Ignore(h => h.Definition);
            });
            village.OwnsMany(v => v.Trees, tree =>
            {
                tree.ToJson();
                tree.Ignore(t => t.Tile);
            });
            village.OwnsMany(v => v.Fields, bed =>
            {
                bed.ToJson();
                bed.Property(b => b.Crop).HasConversion<string>();
                bed.OwnsMany(b=>b.Contributions,a=>{a.Property(x=>x.Step).HasConversion<string>();a.Property(x=>x.Name).HasMaxLength(80);});
            });
            village.OwnsMany(v => v.Research, research =>
            {
                research.ToJson();
                research.OwnsMany(r => r.Hands);
            });
            village.OwnsOne(v => v.Poll, poll =>
            {
                poll.ToJson();
                poll.Ignore(p => p.IsOpen);
                poll.OwnsMany(p => p.Ballots);
            });
            village.OwnsMany(v => v.Forecasts, forecast => forecast.ToJson());
            village.OwnsMany(v => v.Fortifications, wall => { wall.ToJson(); wall.Ignore(f=>f.Blocks);wall.Ignore(f=>f.Strength); });
            village.OwnsMany(v=>v.BuildingDisplayNumbers,n=>n.ToJson());
            village.OwnsOne(v=>v.RaidAdaptation,a=>{a.ToJson();a.OwnsMany(x=>x.Recent);a.OwnsMany(x=>x.Participation);});
            village.OwnsMany(v=>v.RaidPresets,preset=>{preset.ToJson();preset.OwnsOne(p=>p.Settings,settings=>MapRaidSettings(settings));});
            village.OwnsMany(v => v.Raids, raid =>
            {
                raid.ToJson();
                raid.Property(r => r.Source).HasConversion<string>();
                raid.Property(r => r.Enemy).HasConversion<string>();
                raid.Property(r => r.Outcome).HasConversion<string>();
                raid.Ignore(r => r.IsPending);
                raid.OwnsOne(r=>r.Summary);
                raid.OwnsMany(r => r.ApproachPath);
                raid.OwnsMany(r => r.Stolen, good => good.Property(g => g.Kind).HasConversion<string>());
                raid.OwnsMany(r => r.Loot, good => good.Property(g => g.Kind).HasConversion<string>());
            });
            village.OwnsOne(v => v.Wilds, wilds =>
            {
                wilds.ToJson();
                wilds.Ignore(w => w.CaveMouth);
                wilds.Ignore(w => w.Lookout);
                wilds.OwnsMany(w => w.Ravine);
                wilds.OwnsMany(w => w.Passes);
                wilds.OwnsMany(w => w.Obstacles, obstacle =>
                {
                    obstacle.Property(o => o.Kind).HasConversion<string>();
                    obstacle.Ignore(o => o.Tile);
                    obstacle.Ignore(o => o.Center);
                });
            });
            village.HasIndex(v => v.NextRaidAt);
            village.Property(v => v.ChosenTechnology).HasMaxLength(32);
            // Exploration mutates a byte buffer in place. Snapshot the contents, not its reference,
            // so discovered plots remain known on the next request and after reconnecting.
            village.Property(v => v.Explored).Metadata.SetValueComparer(new ValueComparer<byte[]>(
                (left,right)=>left!.SequenceEqual(right!),
                value=>value.Aggregate(0,(hash,part)=>HashCode.Combine(hash,part)),
                value=>value.ToArray()));
            village.Ignore(v => v.TsarSpot);
            village.OwnsOne(v => v.TsarTrip, MapTrip);
            village.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<Citizen>(citizen =>
        {
            citizen.HasKey(c => c.Id);
            citizen.Property(c => c.CurrentTargetPlotId).HasMaxLength(32);
            citizen.HasIndex(c => new { c.VillageId, c.AccountId }).IsUnique();
            citizen.HasIndex(c => new { c.CycleSettled, c.CycleEndsAt });
            citizen.HasIndex(c => c.IsCarrying);
            citizen.OwnsMany(c => c.PersonalGoods, goods => { goods.ToJson(); goods.Property(g=>g.Kind).HasConversion<string>(); });
            citizen.OwnsMany(c => c.Load, load =>
            {
                load.ToJson();
                load.Property(g => g.Kind).HasConversion<string>();
            });
            citizen.OwnsMany(c => c.Skills, skills =>
            {
                skills.ToJson();
                skills.Property(s => s.Kind).HasConversion<string>();
            });
            citizen.OwnsOne(c => c.Appearance, appearance =>
            {
                appearance.ToJson();
                appearance.Property(a => a.Gender).HasConversion<string>();
            });
            citizen.OwnsOne(c => c.Recovery, recovery => recovery.ToJson());
            citizen.OwnsOne(c => c.RescueJourney, rescue => { rescue.ToJson(); rescue.OwnsMany(r => r.Steps); });
            citizen.Property(c => c.Name).HasMaxLength(32);
            citizen.Property(c => c.Subtitle).HasMaxLength(Citizen.SubtitleMaximumLength);
            citizen.Property(c => c.Status).HasConversion<string>().HasMaxLength(16);
            citizen.Property(c => c.RestState).HasConversion<string>().HasMaxLength(16).HasDefaultValue(Desertod.Domain.Rules.RestState.Active);
            citizen.OwnsMany(c=>c.CycleFarmTargets,target=>{target.ToJson();target.Property(t=>t.PlotId).HasMaxLength(40);});
            citizen.Property(c => c.CurrentTaskId).HasMaxLength(32);
            citizen.Property(c => c.CurrentSiteId).HasMaxLength(16);
            citizen.Property(c => c.CurrentTechnologyId).HasMaxLength(32);
            citizen.Property(c => c.Weapon).HasConversion<string>().HasMaxLength(16);
            citizen.Property(c => c.Armor).HasConversion<string>().HasMaxLength(16);
            citizen.Ignore(c => c.RegenerationMultiplier);
            citizen.Ignore(c => c.SleepsUnderTrees);
            citizen.Ignore(c => c.Spot);
            citizen.Ignore(c => c.IdleSpot);
            citizen.OwnsOne(c => c.Trip, MapTrip);
            citizen.HasOne<Village>().WithMany().HasForeignKey(c => c.VillageId);
            citizen.HasOne<Account>().WithMany().HasForeignKey(c => c.AccountId);
            citizen.OwnsOne(c => c.Stats, stats => stats.Ignore(s => s.Total));
            citizen.Ignore(c => c.MaximumEnergy);
            citizen.Ignore(c => c.GraceEndsAt);
            citizen.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<VillageProject>(project =>
        {
            project.HasKey(p => p.Id);
            project.HasIndex(p => new { p.VillageId, p.Status });
            project.Property(p => p.DefinitionId).HasMaxLength(32);
            project.Property(p => p.Status).HasConversion<string>().HasMaxLength(16);
            project.HasOne<Village>().WithMany().HasForeignKey(p => p.VillageId);
            project.HasMany(p => p.Materials).WithOne().HasForeignKey(m => m.VillageProjectId);
            project.HasMany(p => p.Contributions).WithOne().HasForeignKey(c => c.VillageProjectId);
            project.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<ProjectMaterial>(material =>
        {
            material.HasKey(m => new { m.VillageProjectId, m.Kind });
            material.Property(m => m.Kind).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<ProjectContribution>(contribution =>
        {
            contribution.HasKey(c => new { c.VillageProjectId, c.CitizenId, c.Key });
            contribution.Property(c => c.Key).HasMaxLength(32);
            contribution.HasOne<Citizen>().WithMany().HasForeignKey(c => c.CitizenId);
        });

        modelBuilder.Entity<VillageQuest>(quest =>
        {
            quest.HasKey(q => q.Id);
            quest.HasIndex(q => new { q.VillageId, q.Key })
                .IsUnique()
                .HasFilter($"\"{nameof(VillageQuest.Status)}\" = '{nameof(QuestStatus.Active)}'");
            quest.Property(q => q.Kind).HasConversion<string>().HasMaxLength(16);
            quest.Property(q => q.Resource).HasConversion<string>().HasMaxLength(32);
            quest.Property(q => q.Key).HasMaxLength(48);
            quest.Property(q => q.Status).HasConversion<string>().HasMaxLength(16);
            quest.HasOne<Village>().WithMany().HasForeignKey(q => q.VillageId);
            quest.HasMany(q => q.Contributions).WithOne().HasForeignKey(c => c.VillageQuestId);
            quest.Property<uint>("Version").IsRowVersion();
        });

        modelBuilder.Entity<QuestContribution>(contribution =>
        {
            contribution.HasKey(c => new { c.VillageQuestId, c.CitizenId });
            contribution.HasOne<Citizen>().WithMany().HasForeignKey(c => c.CitizenId);
        });

        modelBuilder.Entity<ChatMessage>(message =>
        {
            message.HasKey(m => m.Id);
            message.HasIndex(m => new { m.VillageId, m.SentAt });
            message.Property(m => m.AuthorName).HasMaxLength(ChatMessage.AuthorNameMaximumLength);
            message.Property(m => m.Text).HasMaxLength(ChatMessage.TextMaximumLength);
            message.Ignore(m => m.IsTsar);
            message.HasOne<Village>().WithMany().HasForeignKey(m => m.VillageId);
            message.HasOne<Account>().WithMany().HasForeignKey(m => m.AccountId);
            message.HasOne<Citizen>().WithMany().HasForeignKey(m => m.CitizenId);
        });

        modelBuilder.Entity<ChaplainMessage>(message =>
        {
            message.HasKey(m => m.Id);
            message.HasIndex(m => new { m.VillageId, m.SentAt });
            message.Property(m => m.Text).HasMaxLength(ChatMessage.TextMaximumLength);
            message.HasOne<Village>().WithMany().HasForeignKey(m => m.VillageId);
        });

        modelBuilder.Entity<DungeonRun>(run =>
        {
            run.HasKey(r => r.Id);
            run.HasIndex(r => new { r.VillageId, r.Phase });
            run.Property(r => r.DungeonId).HasMaxLength(32);
            run.Property(r => r.Phase).HasConversion<string>().HasMaxLength(16);
            run.Property(r => r.Outcome).HasConversion<string>().HasMaxLength(16);
            run.Property(r => r.CheckpointJson).HasColumnType("jsonb");
            run.Ignore(r => r.IsActive);
            run.Ignore(r => r.Tsar);
            run.HasOne<Village>().WithMany().HasForeignKey(r => r.VillageId);
            run.OwnsMany(r => r.Heroes, hero =>
            {
                hero.ToJson();
                hero.Property(h => h.Class).HasConversion<string>();
                hero.Property(h => h.Killer).HasConversion<string>();
                hero.PrimitiveCollection(h => h.Broken).ElementType(e => e.HasConversion<string>());
                hero.Ignore(h => h.IsTsar);
                hero.OwnsMany(h => h.Loot, good => good.Property(g => g.Kind).HasConversion<string>());
            });
            run.Property<uint>("Version").IsRowVersion();
        });
    }

    private static void MapRaidSettings<TOwner>(OwnedNavigationBuilder<TOwner, RaidSettings> settings) where TOwner : class
    {
        settings.Property(s=>s.Enemy).HasConversion<string>();
        settings.OwnsMany(s=>s.Species,species=>species.Property(s=>s.Enemy).HasConversion<string>());
    }

    private static void MapTrip<TOwner>(OwnedNavigationBuilder<TOwner, Trip> trip)
        where TOwner : class
    {
        trip.ToJson();
        trip.Ignore(t => t.From);
        trip.Ignore(t => t.To);
        trip.Ignore(t => t.ArrivesAt);
        trip.Ignore(t => t.Duration);
        trip.OwnsMany(t=>t.Waypoints,points=>points.Ignore(p=>p.Point));
    }
}
