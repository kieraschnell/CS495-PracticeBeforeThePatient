using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data.Entities;

namespace PracticeBeforeThePatient.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ScenarioEntity> Scenarios => Set<ScenarioEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CourseEntity> Courses => Set<CourseEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasIndex(e => e.SsoSubject).IsUnique();
            entity.Property(e => e.SsoSubject).HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(50);
        });

        modelBuilder.Entity<ScenarioEntity>(entity =>
        {
            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.CreatedBy).HasMaxLength(256);
            entity.Property(e => e.Title).HasMaxLength(256);
        });

        modelBuilder.Entity<CourseEntity>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(256);
            entity.Property(e => e.CourseCode).HasMaxLength(50);
            entity.HasIndex(e => e.CourseCode).IsUnique();
        });
    }
}
