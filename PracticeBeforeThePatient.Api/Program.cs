using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Data.Entities;
using PracticeBeforeThePatient.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorApp", policy =>
    {
        policy.WithOrigins("https://localhost:7124", "http://localhost:5009", "http://34.58.25.26")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "app.db");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton<DevAccessStore>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    if (!db.Scenarios.Any())
    {
        var scenariosDir = Path.Combine(app.Environment.ContentRootPath, "Data", "scenarios");
        if (Directory.Exists(scenariosDir))
        {
            var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            foreach (var file in Directory.GetFiles(scenariosDir, "*.json"))
            {
                var id = Path.GetFileNameWithoutExtension(file) ?? "";
                var createdByEmail = "admin@ua.edu";
                var json = File.ReadAllText(file);
                var parsed = JsonSerializer.Deserialize<Scenario>(json, jsonOptions);
                var rootJson = JsonSerializer.Serialize(parsed?.Root ?? new Node(), jsonOptions);

                db.Scenarios.Add(new ScenarioEntity
                {
                    Id = id,
                    CreatedByEmail = string.IsNullOrWhiteSpace(parsed?.CreatedBy) ? createdByEmail : parsed.CreatedBy,
                    Title = parsed?.Title ?? id,
                    Description = parsed?.Description ?? "",
                    NodesJson = rootJson,
                    CreatedAtUtc = parsed?.CreatedAt ?? DateTime.UtcNow
                });
            }
            db.SaveChanges();
        }
    }

    if (!db.Users.Any())
    {
        db.Users.AddRange(
            new UserEntity
            {
                SsoSubject = "admin-sso-001",
                Email = "admin@ua.edu",
                Name = "Platform Admin",
                Role = DevAccessStore.AdminRole,
                CreatedAtUtc = DateTime.UtcNow
            },
            new UserEntity
            {
                SsoSubject = "instructor-sso-002",
                Email = "instructor@ua.edu",
                Name = "Jane Doe",
                Role = DevAccessStore.TeacherRole,
                CreatedAtUtc = DateTime.UtcNow
            },
            new UserEntity
            {
                SsoSubject = "student-sso-003",
                Email = "student1@ua.edu",
                Name = "Alice Smith",
                Role = "student",
                CreatedAtUtc = DateTime.UtcNow
            },
            new UserEntity
            {
                SsoSubject = "student-sso-004",
                Email = "student2@ua.edu",
                Name = "Bob Johnson",
                Role = "student",
                CreatedAtUtc = DateTime.UtcNow
            }
        );
        db.SaveChanges();
    }

    var existingUsers = db.Users.ToList();
    var rolesChanged = false;
    foreach (var user in existingUsers)
    {
        var normalizedRole = DevAccessStore.NormalizeRole(user.Role);
        if (string.Equals(user.Email, "admin@ua.edu", StringComparison.OrdinalIgnoreCase))
        {
            normalizedRole = DevAccessStore.AdminRole;
        }

        if (!string.Equals(user.Role, normalizedRole, StringComparison.OrdinalIgnoreCase))
        {
            user.Role = normalizedRole;
            rolesChanged = true;
        }
    }

    if (rolesChanged)
    {
        db.SaveChanges();
    }

    if (!db.Classes.Any())
    {
        var adminUser = db.Users.First(u => u.Email == "admin@ua.edu");

        db.Classes.AddRange(
            new ClassEntity
            {
                Name = "CS 100 - Intro to Computer Science",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = adminUser.Id
            },
            new ClassEntity
            {
                Name = "CS 101 - Programming Fundamentals",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = adminUser.Id
            }
        );
        db.SaveChanges();
    }

    if (!db.ClassTeachers.Any())
    {
        var adminUser = db.Users.First(u => u.Email == "admin@ua.edu");
        var instructor = db.Users.First(u => u.Email == "instructor@ua.edu");
        var cs100 = db.Classes.First(c => c.Name.StartsWith("CS 100"));
        var cs101 = db.Classes.First(c => c.Name.StartsWith("CS 101"));

        db.ClassTeachers.AddRange(
            new ClassTeacherEntity
            {
                ClassId = cs100.Id,
                TeacherUserId = adminUser.Id,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUser.Id
            },
            new ClassTeacherEntity
            {
                ClassId = cs101.Id,
                TeacherUserId = instructor.Id,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUser.Id
            }
        );
        db.SaveChanges();
    }

    if (!db.ClassStudents.Any())
    {
        var adminUser = db.Users.First(u => u.Email == "admin@ua.edu");
        var alice = db.Users.First(u => u.Email == "student1@ua.edu");
        var bob = db.Users.First(u => u.Email == "student2@ua.edu");
        var cs100 = db.Classes.First(c => c.Name.StartsWith("CS 100"));
        var cs101 = db.Classes.First(c => c.Name.StartsWith("CS 101"));

        db.ClassStudents.AddRange(
            new ClassStudentEntity
            {
                ClassId = cs100.Id,
                StudentUserId = alice.Id,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUser.Id
            },
            new ClassStudentEntity
            {
                ClassId = cs100.Id,
                StudentUserId = bob.Id,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUser.Id
            },
            new ClassStudentEntity
            {
                ClassId = cs101.Id,
                StudentUserId = alice.Id,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUser.Id
            }
        );
        db.SaveChanges();
    }

    if (!db.Assignments.Any())
    {
        var adminUser = db.Users.First(u => u.Email == "admin@ua.edu");
        var cs100 = db.Classes.First(c => c.Name.StartsWith("CS 100"));
        var cs101 = db.Classes.First(c => c.Name.StartsWith("CS 101"));
        var scenarios = db.Scenarios.ToList();

        foreach (var scenario in scenarios)
        {
            db.Assignments.Add(new AssignmentEntity
            {
                ClassId = cs100.Id,
                ScenarioId = scenario.Id,
                Name = $"{scenario.Title} Assignment",
                AssignedAtUtc = DateTime.UtcNow,
                DueAtUtc = DateTime.UtcNow.AddDays(14),
                AssignedByUserId = adminUser.Id
            });
        }

        if (scenarios.Count > 0)
        {
            db.Assignments.Add(new AssignmentEntity
            {
                ClassId = cs101.Id,
                ScenarioId = scenarios[0].Id,
                Name = $"{scenarios[0].Title} Assignment",
                AssignedAtUtc = DateTime.UtcNow,
                DueAtUtc = DateTime.UtcNow.AddDays(14),
                AssignedByUserId = adminUser.Id
            });
        }
        db.SaveChanges();
    }

    if (!db.Submissions.Any())
    {
        var alice = db.Users.First(u => u.Email == "student1@ua.edu");
        var firstAssignment = db.Assignments.First();

        db.Submissions.Add(new SubmissionEntity
        {
            AssignmentId = firstAssignment.Id,
            StudentUserId = alice.Id,
            SubmittedAtUtc = DateTime.UtcNow,
            SubmissionText = "{}",
            Grade = 85m
        });
        db.SaveChanges();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("BlazorApp");
app.UseAuthorization();
app.MapControllers();

app.Run();
