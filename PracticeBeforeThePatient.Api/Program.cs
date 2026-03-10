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
        policy.WithOrigins("https://localhost:7124", "http://localhost:5009")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var dataDir = Path.Combine(builder.Environment.ContentRootPath, "Data");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "app.db");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddSingleton<ClassRosterStore>();
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
                var createdBy = "admin@ua.edu";
                var json = File.ReadAllText(file);
                var parsed = JsonSerializer.Deserialize<Scenario>(json, jsonOptions);
                var rootJson = JsonSerializer.Serialize(parsed?.Root ?? new Node(), jsonOptions);

                db.Scenarios.Add(new ScenarioEntity
                {
                    Id = id,
                    CreatedBy = string.IsNullOrWhiteSpace(parsed?.CreatedBy) ? createdBy : parsed.CreatedBy,
                    Title = parsed?.Title ?? id,
                    Description = parsed?.Description ?? "",
                    NodesJson = rootJson,
                    CreatedAt = parsed?.CreatedAt ?? DateTime.UtcNow
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
                Role = DevAccessStore.AdminRole
            },
            new UserEntity
            {
                SsoSubject = "instructor-sso-002",
                Email = "instructor@ua.edu",
                Name = "Jane Doe",
                Role = DevAccessStore.TeacherRole
            },
            new UserEntity
            {
                SsoSubject = "student-sso-003",
                Email = "student1@ua.edu",
                Name = "Alice Smith",
                Role = "student"
            },
            new UserEntity
            {
                SsoSubject = "student-sso-004",
                Email = "student2@ua.edu",
                Name = "Bob Johnson",
                Role = "student"
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

    if (!db.Courses.Any())
    {
        db.Courses.AddRange(
            new CourseEntity
            {
                Title = "Intro to Computer Science",
                CourseCode = "CS 100"
            },
            new CourseEntity
            {
                Title = "Programming Fundamentals",
                CourseCode = "CS 101"
            }
        );
        db.SaveChanges();
    }

    if (!db.CourseInstructors.Any())
    {
        var adminUser = db.Users.First(u => u.Email == "admin@ua.edu");
        var cs100 = db.Courses.First(c => c.CourseCode == "CS 100");
        var cs101 = db.Courses.First(c => c.CourseCode == "CS 101");

        db.CourseInstructors.AddRange(
            new CourseInstructorEntity { CourseId = cs100.Id, InstructorId = adminUser.Id },
            new CourseInstructorEntity { CourseId = cs101.Id, InstructorId = adminUser.Id }
        );
        db.SaveChanges();
    }

    if (!db.Enrollments.Any())
    {
        var alice = db.Users.First(u => u.Email == "student1@ua.edu");
        var bob = db.Users.First(u => u.Email == "student2@ua.edu");
        var cs100 = db.Courses.First(c => c.CourseCode == "CS 100");
        var cs101 = db.Courses.First(c => c.CourseCode == "CS 101");

        db.Enrollments.AddRange(
            new EnrollmentEntity { CourseId = cs100.Id, StudentId = alice.Id },
            new EnrollmentEntity { CourseId = cs100.Id, StudentId = bob.Id },
            new EnrollmentEntity { CourseId = cs101.Id, StudentId = alice.Id }
        );
        db.SaveChanges();
    }

    if (!db.CourseScenarios.Any())
    {
        var cs100 = db.Courses.First(c => c.CourseCode == "CS 100");
        var cs101 = db.Courses.First(c => c.CourseCode == "CS 101");
        var scenarioIds = db.Scenarios.Select(s => s.Id).ToList();

        foreach (var scenarioId in scenarioIds)
        {
            db.CourseScenarios.Add(new CourseScenarioEntity { CourseId = cs100.Id, ScenarioId = scenarioId });
        }

        if (scenarioIds.Count > 0)
        {
            db.CourseScenarios.Add(new CourseScenarioEntity { CourseId = cs101.Id, ScenarioId = scenarioIds[0] });
        }
        db.SaveChanges();
    }

    if (!db.Submissions.Any())
    {
        var alice = db.Users.First(u => u.Email == "student1@ua.edu");
        var cs100 = db.Courses.First(c => c.CourseCode == "CS 100");
        var scenarioId = db.Scenarios.Select(s => s.Id).First();

        db.Submissions.Add(new SubmissionEntity
        {
            StudentId = alice.Id,
            ScenarioId = scenarioId,
            CourseId = cs100.Id,
            AnswersJson = "{}",
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
