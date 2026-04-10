using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Data.Entities;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Data;

public static class AppDbContextInitializer
{
    private static readonly JsonSerializerOptions ScenarioJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task InitializeAsync(
        IServiceProvider services,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        await MigrateWithRetryAsync(services, logger, cancellationToken);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await SeedAsync(db, environment.ContentRootPath, logger, cancellationToken);
    }

    private static async Task MigrateWithRetryAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 10;
        var retryDelay = TimeSpan.FromSeconds(5);

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied successfully.");
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {DelaySeconds} seconds.",
                    attempt,
                    maxAttempts,
                    retryDelay.TotalSeconds);

                await Task.Delay(retryDelay, cancellationToken);
            }
        }

        using var finalScope = services.CreateScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await finalDb.Database.MigrateAsync(cancellationToken);
    }

    private static async Task SeedAsync(
        AppDbContext db,
        string contentRootPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await SeedScenariosAsync(db, contentRootPath, logger, cancellationToken);
        await SeedUsersAsync(db, cancellationToken);
        await NormalizeRolesAsync(db, cancellationToken);
        await SeedClassesAsync(db, cancellationToken);
        await SeedClassTeachersAsync(db, cancellationToken);
        await SeedClassStudentsAsync(db, cancellationToken);
        await SeedAssignmentsAsync(db, cancellationToken);
        await SeedSubmissionsAsync(db, cancellationToken);
    }

    private static async Task SeedScenariosAsync(
        AppDbContext db,
        string contentRootPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var scenariosDir = Path.Combine(contentRootPath, "Data", "scenarios");
        if (!Directory.Exists(scenariosDir))
        {
            return;
        }

        var existingScenarioIds = new HashSet<string>(
            await db.Scenarios
                .Select(s => s.Id)
                .ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.GetFiles(scenariosDir, "*.json"))
        {
            var scenarioId = Path.GetFileNameWithoutExtension(file) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(scenarioId) || existingScenarioIds.Contains(scenarioId))
            {
                continue;
            }

            try
            {
                var json = await File.ReadAllTextAsync(file, cancellationToken);
                var parsed = JsonSerializer.Deserialize<Scenario>(json, ScenarioJsonOptions);
                var rootJson = JsonSerializer.Serialize(parsed?.Root ?? new Node(), ScenarioJsonOptions);

                db.Scenarios.Add(new ScenarioEntity
                {
                    Id = scenarioId,
                    CreatedByEmail = string.IsNullOrWhiteSpace(parsed?.CreatedBy) ? "admin@ua.edu" : parsed.CreatedBy,
                    Title = parsed?.Title ?? scenarioId,
                    Description = parsed?.Description ?? string.Empty,
                    NodesJson = rootJson,
                    CreatedAtUtc = NormalizeToUtc(parsed?.CreatedAt)
                });

                existingScenarioIds.Add(scenarioId);
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Skipping scenario seed file '{ScenarioFile}' because it is invalid JSON.", file);
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static DateTime NormalizeToUtc(DateTime? value)
    {
        if (!value.HasValue || value.Value == default)
        {
            return DateTime.UtcNow;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
            _ => DateTime.UtcNow
        };
    }

    private static async Task SeedUsersAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existingEmails = new HashSet<string>(
            await db.Users
                .Select(u => u.Email)
                .ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var seedUsers = new[]
        {
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
        };

        foreach (var user in seedUsers.Where(user => !existingEmails.Contains(user.Email)))
        {
            db.Users.Add(user);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task NormalizeRolesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existingUsers = await db.Users.ToListAsync(cancellationToken);
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
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedClassesAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var existingClassNames = new HashSet<string>(
            await db.Classes
                .Select(c => c.Name)
                .ToListAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        var adminUserId = await db.Users
            .Where(u => u.Email == "admin@ua.edu")
            .Select(u => u.Id)
            .FirstAsync(cancellationToken);

        var seedClasses = new[]
        {
            new ClassEntity
            {
                Name = "CS 100 - Intro to Computer Science",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = adminUserId
            },
            new ClassEntity
            {
                Name = "CS 101 - Programming Fundamentals",
                CreatedAtUtc = DateTime.UtcNow,
                CreatedByUserId = adminUserId
            }
        };

        foreach (var classEntity in seedClasses.Where(classEntity => !existingClassNames.Contains(classEntity.Name)))
        {
            db.Classes.Add(classEntity);
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedClassTeachersAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var adminUserId = await db.Users
            .Where(u => u.Email == "admin@ua.edu")
            .Select(u => u.Id)
            .FirstAsync(cancellationToken);

        var instructorId = await db.Users
            .Where(u => u.Email == "instructor@ua.edu")
            .Select(u => u.Id)
            .FirstAsync(cancellationToken);

        var classIds = await db.Classes
            .Where(c => c.Name.StartsWith("CS 100") || c.Name.StartsWith("CS 101"))
            .ToDictionaryAsync(c => c.Name, c => c.Id, cancellationToken);

        var existingTeacherPairs = await db.ClassTeachers
            .Select(ct => new { ct.ClassId, ct.TeacherUserId })
            .ToListAsync(cancellationToken);

        var seedAssignments = new[]
        {
            new ClassTeacherEntity
            {
                ClassId = classIds.First(pair => pair.Key.StartsWith("CS 100")).Value,
                TeacherUserId = adminUserId,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUserId
            },
            new ClassTeacherEntity
            {
                ClassId = classIds.First(pair => pair.Key.StartsWith("CS 101")).Value,
                TeacherUserId = instructorId,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUserId
            }
        };

        foreach (var assignment in seedAssignments)
        {
            var exists = existingTeacherPairs.Any(existing =>
                existing.ClassId == assignment.ClassId
                && existing.TeacherUserId == assignment.TeacherUserId);

            if (!exists)
            {
                db.ClassTeachers.Add(assignment);
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedClassStudentsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var adminUserId = await db.Users
            .Where(u => u.Email == "admin@ua.edu")
            .Select(u => u.Id)
            .FirstAsync(cancellationToken);

        var aliceId = await db.Users
            .Where(u => u.Email == "student1@ua.edu")
            .Select(u => u.Id)
            .FirstAsync(cancellationToken);

        var bobId = await db.Users
            .Where(u => u.Email == "student2@ua.edu")
            .Select(u => u.Id)
            .FirstAsync(cancellationToken);

        var classIds = await db.Classes
            .Where(c => c.Name.StartsWith("CS 100") || c.Name.StartsWith("CS 101"))
            .ToDictionaryAsync(c => c.Name, c => c.Id, cancellationToken);

        var existingStudentPairs = await db.ClassStudents
            .Select(cs => new { cs.ClassId, cs.StudentUserId })
            .ToListAsync(cancellationToken);

        var seedEnrollments = new[]
        {
            new ClassStudentEntity
            {
                ClassId = classIds.First(pair => pair.Key.StartsWith("CS 100")).Value,
                StudentUserId = aliceId,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUserId
            },
            new ClassStudentEntity
            {
                ClassId = classIds.First(pair => pair.Key.StartsWith("CS 100")).Value,
                StudentUserId = bobId,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUserId
            },
            new ClassStudentEntity
            {
                ClassId = classIds.First(pair => pair.Key.StartsWith("CS 101")).Value,
                StudentUserId = aliceId,
                AddedAtUtc = DateTime.UtcNow,
                AddedByUserId = adminUserId
            }
        };

        foreach (var enrollment in seedEnrollments)
        {
            var exists = existingStudentPairs.Any(existing =>
                existing.ClassId == enrollment.ClassId
                && existing.StudentUserId == enrollment.StudentUserId);

            if (!exists)
            {
                db.ClassStudents.Add(enrollment);
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedAssignmentsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var adminUserId = await db.Users
            .Where(u => u.Email == "admin@ua.edu")
            .Select(u => u.Id)
            .FirstAsync(cancellationToken);

        var classes = await db.Classes
            .Where(c => c.Name.StartsWith("CS 100") || c.Name.StartsWith("CS 101"))
            .ToDictionaryAsync(c => c.Name, c => c.Id, cancellationToken);

        var scenarios = await db.Scenarios
            .OrderBy(s => s.Id)
            .Select(s => new { s.Id, s.Title })
            .ToListAsync(cancellationToken);

        var existingAssignments = await db.Assignments
            .Select(a => new { a.ClassId, a.ScenarioId, a.Name })
            .ToListAsync(cancellationToken);

        var cs100Id = classes.First(pair => pair.Key.StartsWith("CS 100")).Value;
        foreach (var scenario in scenarios)
        {
            var assignmentName = $"{scenario.Title} Assignment";
            var exists = existingAssignments.Any(existing =>
                existing.ClassId == cs100Id
                && existing.ScenarioId == scenario.Id
                && string.Equals(existing.Name, assignmentName, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                db.Assignments.Add(new AssignmentEntity
                {
                    ClassId = cs100Id,
                    ScenarioId = scenario.Id,
                    Name = assignmentName,
                    AssignedAtUtc = DateTime.UtcNow,
                    DueAtUtc = DateTime.UtcNow.AddDays(14),
                    AssignedByUserId = adminUserId
                });
            }
        }

        var firstScenario = scenarios.FirstOrDefault();
        if (firstScenario is not null)
        {
            var cs101Id = classes.First(pair => pair.Key.StartsWith("CS 101")).Value;
            var assignmentName = $"{firstScenario.Title} Assignment";
            var exists = existingAssignments.Any(existing =>
                existing.ClassId == cs101Id
                && existing.ScenarioId == firstScenario.Id
                && string.Equals(existing.Name, assignmentName, StringComparison.OrdinalIgnoreCase));

            if (!exists)
            {
                db.Assignments.Add(new AssignmentEntity
                {
                    ClassId = cs101Id,
                    ScenarioId = firstScenario.Id,
                    Name = assignmentName,
                    AssignedAtUtc = DateTime.UtcNow,
                    DueAtUtc = DateTime.UtcNow.AddDays(14),
                    AssignedByUserId = adminUserId
                });
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task SeedSubmissionsAsync(AppDbContext db, CancellationToken cancellationToken)
    {
        var aliceId = await db.Users
            .Where(u => u.Email == "student1@ua.edu")
            .Select(u => u.Id)
            .FirstAsync(cancellationToken);

        var firstAssignmentId = await db.Assignments
            .OrderBy(a => a.Id)
            .Select(a => a.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (firstAssignmentId == 0)
        {
            return;
        }

        var submissionExists = await db.Submissions.AnyAsync(
            s => s.AssignmentId == firstAssignmentId && s.StudentUserId == aliceId,
            cancellationToken);

        if (submissionExists)
        {
            return;
        }

        db.Submissions.Add(new SubmissionEntity
        {
            AssignmentId = firstAssignmentId,
            StudentUserId = aliceId,
            SubmittedAtUtc = DateTime.UtcNow,
            SubmissionText = "{}",
            Grade = 85m
        });

        await db.SaveChangesAsync(cancellationToken);
    }
}
