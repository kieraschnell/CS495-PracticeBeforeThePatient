using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data.Entities;

namespace PracticeBeforeThePatient.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ScenarioEntity> Scenarios => Set<ScenarioEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<CourseEntity> Courses => Set<CourseEntity>();
    public DbSet<CourseInstructorEntity> CourseInstructors => Set<CourseInstructorEntity>();
    public DbSet<EnrollmentEntity> Enrollments => Set<EnrollmentEntity>();
    public DbSet<CourseScenarioEntity> CourseScenarios => Set<CourseScenarioEntity>();
    public DbSet<SubmissionEntity> Submissions => Set<SubmissionEntity>();

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

        modelBuilder.Entity<CourseInstructorEntity>(entity =>
        {
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId);
            entity.HasOne(e => e.Instructor).WithMany().HasForeignKey(e => e.InstructorId);
            entity.HasIndex(e => new { e.CourseId, e.InstructorId }).IsUnique();
        });

        modelBuilder.Entity<EnrollmentEntity>(entity =>
        {
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId);
            entity.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId);
            entity.HasIndex(e => new { e.CourseId, e.StudentId }).IsUnique();
        });

        modelBuilder.Entity<CourseScenarioEntity>(entity =>
        {
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId);
            entity.HasOne(e => e.Scenario).WithMany().HasForeignKey(e => e.ScenarioId);
            entity.Property(e => e.ScenarioId).HasMaxLength(128);
            entity.HasIndex(e => new { e.CourseId, e.ScenarioId }).IsUnique();
        });

        modelBuilder.Entity<SubmissionEntity>(entity =>
        {
            entity.HasOne(e => e.Student).WithMany().HasForeignKey(e => e.StudentId);
            entity.HasOne(e => e.Scenario).WithMany().HasForeignKey(e => e.ScenarioId);
            entity.HasOne(e => e.Course).WithMany().HasForeignKey(e => e.CourseId);
            entity.Property(e => e.ScenarioId).HasMaxLength(128);
        });
    }
}
