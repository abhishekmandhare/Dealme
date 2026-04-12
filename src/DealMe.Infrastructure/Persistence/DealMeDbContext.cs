namespace DealMe.Infrastructure.Persistence;

using DealMe.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

public class DealMeDbContext : DbContext
{
    public DealMeDbContext(DbContextOptions<DealMeDbContext> options) : base(options) { }

    /// <summary>
    /// Store DateTimeOffset as ISO-8601 text in SQLite (which has no native datetimeoffset type).
    /// This allows sorting and filtering to work correctly.
    /// PostgreSQL handles DateTimeOffset natively — the converter is a no-op there.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTimeOffset>()
            .HaveConversion<DateTimeOffsetToStringConverter>();
        // Nullable variant uses the same converter — EF Core wraps it automatically
        configurationBuilder.Properties<DateTimeOffset?>()
            .HaveConversion<DateTimeOffsetToStringConverter>();
    }

    public DbSet<DomainEvent> DomainEvents => Set<DomainEvent>();
    public DbSet<Opportunity> Opportunities => Set<Opportunity>();
    public DbSet<PriceWatch> PriceWatches => Set<PriceWatch>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();
    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();
    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<AutomationProviderConfig> AutomationProviderConfigs => Set<AutomationProviderConfig>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.Entity<DomainEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(100).IsRequired();
            e.Property(x => x.AggregateType).HasMaxLength(50).IsRequired();
            e.Property(x => x.Payload).IsRequired();
            e.HasIndex(x => new { x.AggregateType, x.AggregateId, x.Timestamp })
             .HasDatabaseName("IX_Event_Aggregate");
            e.HasIndex(x => x.CorrelationId)
             .HasDatabaseName("IX_Event_Correlation");
        });

        mb.Entity<Opportunity>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasMaxLength(50).IsRequired();
            e.Property(x => x.SourceId).HasMaxLength(255).IsRequired();
            e.Property(x => x.Title).HasMaxLength(500).IsRequired();
            e.Property(x => x.Url).HasMaxLength(2048).IsRequired();
            e.Property(x => x.ImageUrl).HasMaxLength(2048);
            e.Property(x => x.Tags).IsRequired().HasDefaultValue("[]");
            e.Property(x => x.Metadata).IsRequired().HasDefaultValue("{}");
            e.HasIndex(x => new { x.Source, x.SourceId }).IsUnique()
             .HasDatabaseName("IX_Opp_Source_SourceId");
            e.HasIndex(x => new { x.Status, x.CreatedAt })
             .HasDatabaseName("IX_Opp_Status_Created");
            e.HasIndex(x => x.ExpiresAt)
             .HasDatabaseName("IX_Opp_ExpiresAt");
        });

        mb.Entity<PriceWatch>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.SearchQuery).HasMaxLength(500).IsRequired();
            e.Property(x => x.CheapestUrl).HasMaxLength(2048);
            e.Property(x => x.CheapestRetailer).HasMaxLength(100);
            e.Property(x => x.CdioWatchId).HasMaxLength(100);
            e.HasIndex(x => x.IsActive).HasDatabaseName("IX_PW_Active");
        });

        mb.Entity<UserPreferences>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Timezone).HasMaxLength(50).IsRequired();
            e.Property(x => x.EnabledCategories).IsRequired().HasDefaultValue("[]");
            e.Property(x => x.InterestTags).IsRequired().HasDefaultValue("[]");

            var timeConverter = new ValueConverter<TimeOnly?, string?>(
                v => v.HasValue ? v.Value.ToString("HH:mm:ss") : null,
                v => v != null ? TimeOnly.Parse(v) : null);

            e.Property(x => x.QuietHoursStart).HasConversion(timeConverter);
            e.Property(x => x.QuietHoursEnd).HasConversion(timeConverter);
        });

        mb.Entity<NotificationChannel>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.AppriseUrl).HasMaxLength(500).IsRequired();
        });

        mb.Entity<AutomationRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Provider).HasMaxLength(50).IsRequired();
            e.Property(x => x.Task).HasMaxLength(50).IsRequired();
            e.HasIndex(x => new { x.Provider, x.StartedAt })
             .HasDatabaseName("IX_AutoRun_Provider_Started");
        });

        mb.Entity<AutomationProviderConfig>(e =>
        {
            e.HasKey(x => x.ProviderId);
            e.Property(x => x.ProviderId).HasMaxLength(50).IsRequired();
            e.Property(x => x.AccountLabel).HasMaxLength(100);
            e.Property(x => x.CronSchedule).HasMaxLength(100).IsRequired()
             .HasDefaultValue("0 21 * * *");
            e.Property(x => x.MobileSearchCount).HasDefaultValue(20);
        });
    }
}
