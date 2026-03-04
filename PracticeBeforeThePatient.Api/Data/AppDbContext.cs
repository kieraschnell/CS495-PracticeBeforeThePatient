using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data.Entities;

namespace PracticeBeforeThePatient.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<ScenarioEntity> Scenarios => Set<ScenarioEntity>();
}
