using LtcCognitive.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace LtcCognitive.Api.Data;

public sealed class LtcDbContext(DbContextOptions<LtcDbContext> options) : DbContext(options)
{
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<CognitiveDomain> CognitiveDomains => Set<CognitiveDomain>();
    public DbSet<GameDefinition> Games => Set<GameDefinition>();
    public DbSet<GameDomainMapping> GameDomainMappings => Set<GameDomainMapping>();
    public DbSet<AssessmentSession> AssessmentSessions => Set<AssessmentSession>();
    public DbSet<TrialEvent> TrialEvents => Set<TrialEvent>();
    public DbSet<DerivedMetric> DerivedMetrics => Set<DerivedMetric>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("cognitive");
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.HasIndex(x => x.AnonymousCode).IsUnique();
            entity.Property(x => x.AnonymousCode).HasMaxLength(64);
            entity.Property(x => x.EducationBand).HasMaxLength(32);
        });
        modelBuilder.Entity<Device>(entity => entity.HasIndex(x => x.InstallationId).IsUnique());
        modelBuilder.Entity<CognitiveDomain>(entity => entity.HasIndex(x => x.Code).IsUnique());
        modelBuilder.Entity<GameDefinition>(entity => entity.HasIndex(x => x.Code).IsUnique());
        modelBuilder.Entity<GameDomainMapping>(entity =>
        {
            entity.HasKey(x => new { x.GameId, x.DomainId, x.MappingVersion });
            entity.Property(x => x.Weight).HasPrecision(5, 4);
        });
        modelBuilder.Entity<AssessmentSession>(entity =>
        {
            entity.HasIndex(x => new { x.ParticipantId, x.StartedAtUtc });
            entity.ToTable(t => t.HasCheckConstraint("ck_session_duration", "\"DurationMs\" >= 0"));
        });
        modelBuilder.Entity<TrialEvent>(entity =>
        {
            entity.HasIndex(x => new { x.SessionId, x.TrialIndex }).IsUnique();
            entity.Property(x => x.StimulusJson).HasColumnType("jsonb");
        });
        modelBuilder.Entity<DerivedMetric>(entity =>
            entity.HasIndex(x => new { x.SessionId, x.MetricCode, x.DomainId }).IsUnique());
        SeedReferenceData(modelBuilder);
    }

    private static void SeedReferenceData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CognitiveDomain>().HasData(
            new CognitiveDomain { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Code = "attention_inhibition", NameZhTw = "注意力與抑制控制" },
            new CognitiveDomain { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Code = "processing_speed", NameZhTw = "處理速度與視覺搜尋" },
            new CognitiveDomain { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Code = "executive_reasoning", NameZhTw = "執行功能與數字推理" },
            new CognitiveDomain { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Code = "visual_working_memory", NameZhTw = "視覺空間工作記憶" },
            new CognitiveDomain { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Code = "visuospatial_planning", NameZhTw = "視空間規劃與問題解決" });
        modelBuilder.Entity<GameDefinition>().HasData(
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), Code = "stroop_color_match", NameZhTw = "顏色文字判斷" },
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), Code = "number_order", NameZhTw = "數字排序" },
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), Code = "number_sum", NameZhTw = "數字組合" },
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), Code = "card_memory", NameZhTw = "翻卡牌" },
            new GameDefinition { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), Code = "pipe_rotation", NameZhTw = "旋轉接水管" });
    }
}
