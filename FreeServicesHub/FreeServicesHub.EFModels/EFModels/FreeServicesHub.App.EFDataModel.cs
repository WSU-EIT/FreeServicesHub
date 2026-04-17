using Microsoft.EntityFrameworkCore;

namespace FreeServicesHub.EFModels.EFModels;

public partial class EFDataModel
{
    public virtual DbSet<Agent> Agents { get; set; }

    public virtual DbSet<RegistrationKey> RegistrationKeys { get; set; }

    public virtual DbSet<ApiClientToken> ApiClientTokens { get; set; }

    public virtual DbSet<AgentHeartbeat> AgentHeartbeats { get; set; }

    public virtual DbSet<HubJob> HubJobs { get; set; }

    public virtual DbSet<CiCdTokenUsage> CiCdTokenUsages { get; set; }

    // The OnConfiguring override is only commented out and used to build the migration scripts.
    //protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    // => optionsBuilder.UseSqlite("Data Source=C:\\Working\\FreeServicesHub.db");
    // => optionsBuilder.UseMySQL("Server=localhost;Database=FreeServicesHub;User=admin;Password=admin");
    // => optionsBuilder.UseNpgsql("Host=localhost;Database=FreeServicesHub;Username=postgres;Password=admin");
    //=> optionsBuilder.UseSqlServer("Data Source=(local);Initial Catalog=FreeServicesHub;Persist Security Info=True;User ID=sa;Password=saPassword;MultipleActiveResultSets=True;TrustServerCertificate=True;");

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Agent>(entity =>
        {
            entity.Property(e => e.AgentId).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Hostname).HasMaxLength(255);
            entity.Property(e => e.OperatingSystem).HasMaxLength(100);
            entity.Property(e => e.Architecture).HasMaxLength(50);
            entity.Property(e => e.AgentVersion).HasMaxLength(50);
            entity.Property(e => e.DotNetVersion).HasMaxLength(50);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.LastHeartbeat).HasColumnType("datetime");
            entity.Property(e => e.RegisteredAt).HasColumnType("datetime");
            entity.Property(e => e.RegisteredBy).HasMaxLength(255);
            entity.Property(e => e.Added).HasColumnType("datetime");
            entity.Property(e => e.AddedBy).HasMaxLength(100);
            entity.Property(e => e.LastModified).HasColumnType("datetime");
            entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
            entity.Property(e => e.DeletedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<RegistrationKey>(entity =>
        {
            entity.Property(e => e.RegistrationKeyId).ValueGeneratedNever();
            entity.Property(e => e.KeyHash).HasMaxLength(100);
            entity.Property(e => e.KeyPrefix).HasMaxLength(20);
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.UsedAt).HasColumnType("datetime");
            entity.Property(e => e.Created).HasColumnType("datetime");
            entity.Property(e => e.CreatedBy).HasMaxLength(255);
        });

        modelBuilder.Entity<ApiClientToken>(entity =>
        {
            entity.Property(e => e.ApiClientTokenId).ValueGeneratedNever();
            entity.Property(e => e.TokenHash).HasMaxLength(100);
            entity.Property(e => e.TokenPrefix).HasMaxLength(20);
            entity.Property(e => e.Created).HasColumnType("datetime");
            entity.Property(e => e.RevokedAt).HasColumnType("datetime");
            entity.Property(e => e.RevokedBy).HasMaxLength(255);
        });

        modelBuilder.Entity<AgentHeartbeat>(entity =>
        {
            entity.HasKey(e => e.HeartbeatId);

            entity.Property(e => e.HeartbeatId).ValueGeneratedNever();
            entity.Property(e => e.Timestamp).HasColumnType("datetime");
        });

        modelBuilder.Entity<HubJob>(entity =>
        {
            entity.Property(e => e.HubJobId).ValueGeneratedNever();
            entity.Property(e => e.JobType).HasMaxLength(100);
            entity.Property(e => e.Status).HasMaxLength(50);
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.LastModifiedBy).HasMaxLength(100);
            entity.Property(e => e.Created).HasColumnType("datetime");
            entity.Property(e => e.StartedAt).HasColumnType("datetime");
            entity.Property(e => e.CompletedAt).HasColumnType("datetime");
            entity.Property(e => e.LastModified).HasColumnType("datetime");
            entity.Property(e => e.DeletedAt).HasColumnType("datetime");
        });

        modelBuilder.Entity<CiCdTokenUsage>(entity =>
        {
            entity.Property(e => e.CiCdTokenUsageId).ValueGeneratedNever();
            entity.Property(e => e.TokenHash).HasMaxLength(100);
            entity.Property(e => e.TokenPrefix).HasMaxLength(20);
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime");
            entity.Property(e => e.Created).HasColumnType("datetime");
            entity.Property(e => e.InvalidatedAt).HasColumnType("datetime");
        });
    }
}
