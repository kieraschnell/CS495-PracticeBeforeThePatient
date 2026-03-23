using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data.Entities;

namespace PracticeBeforeThePatient.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ClassEntity> Classes => Set<ClassEntity>();
    public DbSet<ClassTeacherEntity> ClassTeachers => Set<ClassTeacherEntity>();
    public DbSet<ClassStudentEntity> ClassStudents => Set<ClassStudentEntity>();
    public DbSet<ScenarioEntity> Scenarios => Set<ScenarioEntity>();
    public DbSet<AssignmentEntity> Assignments => Set<AssignmentEntity>();
    public DbSet<SubmissionEntity> Submissions => Set<SubmissionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>(entity =>
        {
            entity.HasIndex(e => e.SsoSubject).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.SsoSubject).HasMaxLength(256);
            entity.Property(e => e.Email).HasMaxLength(256);
            entity.Property(e => e.Name).HasMaxLength(256);
            entity.Property(e => e.Role).HasMaxLength(50);
        });

        modelBuilder.Entity<ClassEntity>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(256);

            entity.HasOne(e => e.CreatedBy)
                  .WithMany(u => u.CreatedClasses)
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClassTeacherEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ClassId, e.TeacherUserId }).IsUnique();

            entity.HasOne(e => e.Class)
                  .WithMany(c => c.Teachers)
                  .HasForeignKey(e => e.ClassId);

            entity.HasOne(e => e.Teacher)
                  .WithMany(u => u.TeachingAssignments)
                  .HasForeignKey(e => e.TeacherUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AddedBy)
                  .WithMany()
                  .HasForeignKey(e => e.AddedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ClassStudentEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ClassId, e.StudentUserId }).IsUnique();

            entity.HasOne(e => e.Class)
                  .WithMany(c => c.Students)
                  .HasForeignKey(e => e.ClassId);

            entity.HasOne(e => e.Student)
                  .WithMany(u => u.StudentEnrollments)
                  .HasForeignKey(e => e.StudentUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AddedBy)
                  .WithMany()
                  .HasForeignKey(e => e.AddedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ScenarioEntity>(entity =>
        {
            entity.Property(e => e.Id).HasMaxLength(128);
            entity.Property(e => e.CreatedByEmail).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Title).HasMaxLength(256).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.NodesJson).IsRequired();
        });

        modelBuilder.Entity<AssignmentEntity>(entity =>
        {
            entity.HasIndex(e => new { e.ClassId, e.ScenarioId }).IsUnique();
            entity.Property(e => e.ScenarioId).HasMaxLength(128);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();

            entity.HasOne(e => e.Class)
                  .WithMany(c => c.Assignments)
                  .HasForeignKey(e => e.ClassId);

            entity.HasOne(e => e.Scenario)
                  .WithMany(s => s.Assignments)
                  .HasForeignKey(e => e.ScenarioId);

            entity.HasOne(e => e.AssignedBy)
                  .WithMany(u => u.AssignedAssignments)
                  .HasForeignKey(e => e.AssignedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SubmissionEntity>(entity =>
        {
            entity.HasIndex(e => new { e.AssignmentId, e.StudentUserId }).IsUnique();
            entity.Property(e => e.SubmissionText).IsRequired();

            entity.HasOne(e => e.Assignment)
                  .WithMany(a => a.Submissions)
                  .HasForeignKey(e => e.AssignmentId);

            entity.HasOne(e => e.Student)
                  .WithMany(u => u.SubmittedSubmissions)
                  .HasForeignKey(e => e.StudentUserId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.GradedBy)
                  .WithMany(u => u.GradedSubmissions)
                  .HasForeignKey(e => e.GradedByUserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
