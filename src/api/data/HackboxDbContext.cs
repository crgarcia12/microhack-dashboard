using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class HackboxDbContext : DbContext
{
    public HackboxDbContext(DbContextOptions<HackboxDbContext> options) : base(options) { }

    public DbSet<TeamProgressEntity> TeamProgress => Set<TeamProgressEntity>();
    public DbSet<TimerStateEntity> TimerStates => Set<TimerStateEntity>();
    public DbSet<ChallengeTimeEntity> ChallengeTimes => Set<ChallengeTimeEntity>();
    public DbSet<AuthSessionEntity> AuthSessions => Set<AuthSessionEntity>();
    public DbSet<TeamEntity> Teams => Set<TeamEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CredentialCategoryEntity> CredentialCategories => Set<CredentialCategoryEntity>();
    public DbSet<CredentialItemEntity> CredentialItems => Set<CredentialItemEntity>();
    public DbSet<HackStateEntity> HackState => Set<HackStateEntity>();
    public DbSet<HackConfigEntity> HackConfig => Set<HackConfigEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimerStateEntity>(entity =>
        {
            entity.HasMany(e => e.ChallengeTimes)
                  .WithOne(e => e.TimerState)
                  .HasForeignKey(e => e.TeamName)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChallengeTimeEntity>(entity =>
        {
            entity.HasIndex(e => new { e.TeamName, e.ChallengeNumber }).IsUnique();
        });

        modelBuilder.Entity<AuthSessionEntity>(entity =>
        {
            entity.HasIndex(e => e.Username);
        });

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasIndex(e => e.TeamName);
        });

        modelBuilder.Entity<CredentialCategoryEntity>(entity =>
        {
            entity.HasIndex(e => e.TeamName);
            entity.HasMany(e => e.Credentials)
                  .WithOne(e => e.Category)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
