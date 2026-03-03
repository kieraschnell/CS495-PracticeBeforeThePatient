using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Core.Models;

namespace PracticeBeforeThePatient.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Scenario> Scenarios { get; set; }
    public DbSet<Class> Classes { get; set; }
    public DbSet<Student> Students { get; set; }
    public DbSet<Instructor> Instructors { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Scenario
        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.ScenarioJson).IsRequired();
        });

        // Configure Class
        modelBuilder.Entity<Class>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            
            // Relationship: Class has one Instructor
            entity.HasOne(c => c.Instructor)
                  .WithMany(i => i.Classes)
                  .HasForeignKey(c => c.InstructorId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Relationship: Class has many Students
            entity.HasMany(c => c.Students)
                  .WithOne(s => s.Class)
                  .HasForeignKey(s => s.ClassId)
                  .OnDelete(DeleteBehavior.SetNull);

            // Store AllowedScenarioIds as JSON
            entity.Property(e => e.AllowedScenarioIds)
                  .HasConversion(
                      v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                      v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>()
                  );
        });

        // Configure Student
        modelBuilder.Entity<Student>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // Configure Instructor
        modelBuilder.Entity<Instructor>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}
