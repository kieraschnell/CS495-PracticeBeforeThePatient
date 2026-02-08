using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Core.Models;

namespace PracticeBeforeThePatient.Api.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Scenario> Scenarios { get; set; }
    public DbSet<Node> Nodes { get; set; }
    public DbSet<Choice> Choices { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Scenario
        modelBuilder.Entity<Scenario>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Root)
                .WithMany()
                .HasForeignKey(e => e.RootNodeId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Configure Node
        modelBuilder.Entity<Node>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasMany(e => e.Choices)
                .WithOne()
                .HasForeignKey(c => c.NodeId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure Choice
        modelBuilder.Entity<Choice>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Next)
                .WithMany()
                .HasForeignKey(e => e.NextNodeId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}