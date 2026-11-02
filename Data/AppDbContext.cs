using BuildWorkflowRelease.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BuildWorkflowRelease.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RepositoryConfig> Repositories { get; set; } = null!;
    public DbSet<BuildConfig> Builds { get; set; } = null!;
    public DbSet<SftpConfig> SftpConfigs { get; set; } = null!;
    public DbSet<ReleaseProfile> ReleaseProfiles { get; set; } = null!;
    public DbSet<ReleaseRun> ReleaseRuns { get; set; } = null!;
    public DbSet<ReleaseRunLog> ReleaseRunLogs { get; set; } = null!;
    public DbSet<ApiHealthCheck> ApiHealthChecks { get; set; } = null!;
    public DbSet<HealthCheckLog> HealthCheckLogs { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<BuildConfig>()
            .HasOne(b => b.Repository)
            .WithMany()
            .HasForeignKey(b => b.RepositoryId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReleaseProfile>()
            .HasOne(p => p.BuildConfig)
            .WithMany()
            .HasForeignKey(p => p.BuildConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReleaseProfile>()
            .HasOne(p => p.SftpConfig)
            .WithMany()
            .HasForeignKey(p => p.SftpConfigId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReleaseRun>()
            .HasOne(r => r.ReleaseProfile)
            .WithMany()
            .HasForeignKey(r => r.ReleaseProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ReleaseRunLog>()
            .HasOne(l => l.ReleaseRun)
            .WithMany(r => r.Logs)
            .HasForeignKey(l => l.ReleaseRunId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ApiHealthCheck>()
            .HasOne(c => c.SftpConfig)
            .WithMany()
            .HasForeignKey(c => c.SftpConfigId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<HealthCheckLog>()
            .HasOne(l => l.ApiHealthCheck)
            .WithMany()
            .HasForeignKey(l => l.ApiHealthCheckId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();
    }
}