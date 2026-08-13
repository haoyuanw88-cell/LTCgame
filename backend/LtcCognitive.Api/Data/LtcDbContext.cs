using LtcCognitive.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LtcCognitive.Api.Data;

public sealed class LtcDbContext(DbContextOptions<LtcDbContext> options) : DbContext(options)
{
    public DbSet<Player> Players => Set<Player>();
    public DbSet<CognitiveDomain> CognitiveDomains => Set<CognitiveDomain>();
    public DbSet<GameDefinition> Games => Set<GameDefinition>();
    public DbSet<AssessmentSession> AssessmentSessions => Set<AssessmentSession>();
    public DbSet<TrialEvent> TrialEvents => Set<TrialEvent>();
    public DbSet<DerivedMetric> DerivedMetrics => Set<DerivedMetric>();
    public DbSet<CoinTransaction> CoinTransactions => Set<CoinTransaction>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<PlayerInventory> PlayerInventories => Set<PlayerInventory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigurePlayer(modelBuilder);
        ConfigureAssessment(modelBuilder);
        ConfigureEconomy(modelBuilder);
        SeedReferenceData(modelBuilder);
    }

    static void ConfigurePlayer(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.ToTable("player", "ltc", t =>
            {
                t.HasCheckConstraint("ck_player_status", "status IN ('active', 'suspended', 'deleted')");
                t.HasCheckConstraint("ck_player_birth_year", "birth_year IS NULL OR birth_year BETWEEN 1900 AND 2100");
            });
            entity.HasKey(x => x.PlayerId).HasName("pk_player");
            entity.Property(x => x.PlayerId).HasColumnName("player_id").UseIdentityAlwaysColumn();
            entity.Property(x => x.PlayerCode).HasColumnName("player_code").HasMaxLength(16);
            entity.Property(x => x.AuthProvider).HasColumnName("auth_provider").HasMaxLength(24);
            entity.Property(x => x.AuthSubjectHash).HasColumnName("auth_subject_hash").HasColumnType("character(64)");
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(40);
            entity.Property(x => x.BirthYear).HasColumnName("birth_year");
            entity.Property(x => x.EducationLevelCode).HasColumnName("education_level_code").HasMaxLength(24);
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(16);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(x => x.LastLoginAtUtc).HasColumnName("last_login_at_utc");
            entity.HasIndex(x => x.PlayerCode).IsUnique().HasDatabaseName("uq_player_code");
            entity.HasIndex(x => new { x.AuthProvider, x.AuthSubjectHash }).IsUnique().HasDatabaseName("uq_player_auth_subject");
        });
    }

    static void ConfigureAssessment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CognitiveDomain>(entity =>
        {
            entity.ToTable("cognitive_domain", "cognitive");
            entity.HasKey(x => x.Id).HasName("pk_cognitive_domain");
            entity.Property(x => x.Id).HasColumnName("domain_id");
            entity.Property(x => x.Code).HasColumnName("domain_code").HasMaxLength(32);
            entity.Property(x => x.NameZhTw).HasColumnName("name_zh_tw").HasMaxLength(40);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(300);
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_cognitive_domain_code");
        });
        modelBuilder.Entity<GameDefinition>(entity =>
        {
            entity.ToTable("game", "cognitive");
            entity.HasKey(x => x.Id).HasName("pk_game");
            entity.Property(x => x.Id).HasColumnName("game_id");
            entity.Property(x => x.Code).HasColumnName("game_code").HasMaxLength(32);
            entity.Property(x => x.NameZhTw).HasColumnName("name_zh_tw").HasMaxLength(50);
            entity.Property(x => x.DomainId).HasColumnName("domain_id");
            entity.Property(x => x.MappingVersion).HasColumnName("mapping_version").HasMaxLength(16);
            entity.Property(x => x.EvidenceNote).HasColumnName("evidence_note").HasMaxLength(500);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("uq_game_code");
            entity.HasOne(x => x.Domain).WithMany(x => x.Games).HasForeignKey(x => x.DomainId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AssessmentSession>(entity =>
        {
            entity.ToTable("assessment_session", "cognitive", t =>
                t.HasCheckConstraint("ck_assessment_time", "ended_at_utc >= started_at_utc"));
            entity.HasKey(x => x.Id).HasName("pk_assessment_session");
            entity.Property(x => x.Id).HasColumnName("session_id").ValueGeneratedNever();
            entity.Property(x => x.PlayerId).HasColumnName("player_id");
            entity.Property(x => x.GameId).HasColumnName("game_id");
            entity.Property(x => x.TaskVersion).HasColumnName("task_version").HasMaxLength(16);
            entity.Property(x => x.SchemaVersion).HasColumnName("schema_version").HasMaxLength(16);
            entity.Property(x => x.StartedAtUtc).HasColumnName("started_at_utc");
            entity.Property(x => x.EndedAtUtc).HasColumnName("ended_at_utc");
            entity.Property(x => x.CompletionStatus).HasColumnName("completion_status").HasMaxLength(16);
            entity.Property(x => x.ExitReason).HasColumnName("exit_reason").HasMaxLength(80);
            entity.Property(x => x.ReceivedAtUtc).HasColumnName("received_at_utc");
            entity.HasIndex(x => new { x.PlayerId, x.StartedAtUtc }).HasDatabaseName("ix_assessment_player_started");
            entity.HasOne(x => x.Player).WithMany(x => x.Assessments).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Game).WithMany(x => x.Sessions).HasForeignKey(x => x.GameId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TrialEvent>(entity =>
        {
            entity.ToTable("trial_event", "cognitive");
            entity.HasKey(x => new { x.SessionId, x.TrialIndex }).HasName("pk_trial_event");
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.TrialIndex).HasColumnName("trial_index");
            entity.Property(x => x.TrialType).HasColumnName("trial_type").HasMaxLength(32);
            entity.Property(x => x.StimulusJson).HasColumnName("stimulus_json").HasColumnType("jsonb");
            entity.Property(x => x.ExpectedResponse).HasColumnName("expected_response").HasMaxLength(40);
            entity.Property(x => x.ActualResponse).HasColumnName("actual_response").HasMaxLength(40);
            entity.Property(x => x.IsCorrect).HasColumnName("is_correct");
            entity.Property(x => x.ReactionTimeMs).HasColumnName("reaction_time_ms");
            entity.Property(x => x.PresentationDurationMs).HasColumnName("presentation_duration_ms");
            entity.HasOne(x => x.Session).WithMany(x => x.Trials).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<DerivedMetric>(entity =>
        {
            entity.ToTable("derived_metric", "cognitive");
            entity.HasKey(x => x.MetricId).HasName("pk_derived_metric");
            entity.Property(x => x.MetricId).HasColumnName("metric_id").UseIdentityAlwaysColumn();
            entity.Property(x => x.SessionId).HasColumnName("session_id");
            entity.Property(x => x.DomainId).HasColumnName("domain_id");
            entity.Property(x => x.MetricCode).HasColumnName("metric_code").HasMaxLength(40);
            entity.Property(x => x.Value).HasColumnName("value");
            entity.Property(x => x.Unit).HasColumnName("unit").HasMaxLength(16);
            entity.Property(x => x.CalculationVersion).HasColumnName("calculation_version").HasMaxLength(16);
            entity.Property(x => x.QualityFlag).HasColumnName("quality_flag").HasMaxLength(24);
            entity.HasIndex(x => new { x.SessionId, x.DomainId, x.MetricCode }).IsUnique().HasDatabaseName("uq_metric_session_domain_code");
            entity.HasOne(x => x.Session).WithMany(x => x.Metrics).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Domain).WithMany().HasForeignKey(x => x.DomainId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    static void ConfigureEconomy(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CoinTransaction>(entity =>
        {
            entity.ToTable("coin_transaction", "ltc", t => t.HasCheckConstraint("ck_coin_amount_nonzero", "amount <> 0"));
            entity.HasKey(x => x.TransactionId).HasName("pk_coin_transaction");
            entity.Property(x => x.TransactionId).HasColumnName("transaction_id").UseIdentityAlwaysColumn();
            entity.Property(x => x.PlayerId).HasColumnName("player_id");
            entity.Property(x => x.Amount).HasColumnName("amount");
            entity.Property(x => x.TransactionType).HasColumnName("transaction_type").HasMaxLength(24);
            entity.Property(x => x.ReferenceKey).HasColumnName("reference_key").HasMaxLength(64);
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.HasIndex(x => new { x.PlayerId, x.TransactionType, x.ReferenceKey }).IsUnique().HasDatabaseName("uq_coin_source");
            entity.HasOne(x => x.Player).WithMany(x => x.CoinTransactions).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("item", "ltc", t => t.HasCheckConstraint("ck_item_price", "price >= 0"));
            entity.HasKey(x => x.ItemId).HasName("pk_item");
            entity.Property(x => x.ItemId).HasColumnName("item_id").UseIdentityAlwaysColumn();
            entity.Property(x => x.ItemCode).HasColumnName("item_code").HasMaxLength(32);
            entity.Property(x => x.ItemType).HasColumnName("item_type").HasMaxLength(24);
            entity.Property(x => x.NameZhTw).HasColumnName("name_zh_tw").HasMaxLength(50);
            entity.Property(x => x.Price).HasColumnName("price");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.HasIndex(x => x.ItemCode).IsUnique().HasDatabaseName("uq_item_code");
        });
        modelBuilder.Entity<PlayerInventory>(entity =>
        {
            entity.ToTable("player_inventory", "ltc", t => t.HasCheckConstraint("ck_inventory_quantity", "quantity >= 0"));
            entity.HasKey(x => new { x.PlayerId, x.ItemId }).HasName("pk_player_inventory");
            entity.Property(x => x.PlayerId).HasColumnName("player_id");
            entity.Property(x => x.ItemId).HasColumnName("item_id");
            entity.Property(x => x.Quantity).HasColumnName("quantity");
            entity.Property(x => x.AcquiredAtUtc).HasColumnName("acquired_at_utc");
            entity.HasOne(x => x.Player).WithMany(x => x.Inventory).HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.Item).WithMany(x => x.Owners).HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    static void SeedReferenceData(ModelBuilder modelBuilder)
    {
        var attention = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var speed = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var executive = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var memory = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var spatial = Guid.Parse("10000000-0000-0000-0000-000000000005");
        modelBuilder.Entity<CognitiveDomain>().HasData(
            new CognitiveDomain { Id = attention, Code = "attention_inhibition", NameZhTw = "注意力與抑制控制" },
            new CognitiveDomain { Id = speed, Code = "processing_speed", NameZhTw = "處理速度與視覺搜尋" },
            new CognitiveDomain { Id = executive, Code = "executive_reasoning", NameZhTw = "執行功能與數字推理" },
            new CognitiveDomain { Id = memory, Code = "visual_working_memory", NameZhTw = "視覺工作記憶" },
            new CognitiveDomain { Id = spatial, Code = "visuospatial_planning", NameZhTw = "視空間規劃與問題解決" });
        modelBuilder.Entity<GameDefinition>().HasData(
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Code = "stroop_color_match", NameZhTw = "顏色文字判斷", DomainId = attention, MappingVersion = "1.0", EvidenceNote = "Stroop 作業：選擇性注意與反應抑制。" },
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Code = "number_order", NameZhTw = "數字由小到大", DomainId = speed, MappingVersion = "1.0", EvidenceNote = "視覺搜尋、排序與處理速度。" },
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Code = "number_sum", NameZhTw = "數字組合加總", DomainId = executive, MappingVersion = "1.0", EvidenceNote = "工作記憶、策略選擇與數字推理。" },
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Code = "card_memory", NameZhTw = "翻卡牌", DomainId = memory, MappingVersion = "1.0", EvidenceNote = "視覺位置的編碼、保持與提取。" },
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), Code = "pipe_rotation", NameZhTw = "旋轉接水管", DomainId = spatial, MappingVersion = "1.0", EvidenceNote = "心像旋轉、空間規劃與問題解決。" });
    }
}
