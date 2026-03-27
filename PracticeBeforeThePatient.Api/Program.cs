using System.Text.Json;
using System.Text.Json.Serialization;
using System.Data.Common;
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
    EnsureCurrentDatabaseSchema(db, dbPath, app.Logger);
    EnsureAssignmentsSchema(db);

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

static void EnsureAssignmentsSchema(AppDbContext db)
{
    db.Database.OpenConnection();

    try
    {
        var connection = db.Database.GetDbConnection();
        if (!TableExists(connection, "Assignments"))
        {
            return;
        }

        var columns = GetTableColumns(connection, "Assignments");
        var needsAssignedAtColumn = !columns.Contains("AssignedAtUtc");
        var hasUniqueClassScenarioConstraint = HasUniqueClassScenarioConstraint(connection);

        if (!needsAssignedAtColumn && !hasUniqueClassScenarioConstraint)
        {
            return;
        }

        RebuildAssignmentsTable(connection, columns.Contains("AssignedAtUtc"));
    }
    finally
    {
        db.Database.CloseConnection();
    }
}

static void EnsureCurrentDatabaseSchema(AppDbContext db, string dbPath, ILogger logger)
{
    db.Database.OpenConnection();

    try
    {
        var connection = db.Database.GetDbConnection();
        var hasAnyTables = HasAnyUserTables(connection);

        if (!hasAnyTables)
        {
            db.Database.CloseConnection();
            db.Database.EnsureCreated();
            return;
        }

        if (!RequiresDatabaseReset(connection))
        {
            return;
        }
    }
    finally
    {
        db.Database.CloseConnection();
    }

    BackupLegacyDatabaseFiles(dbPath, logger);
    db.Database.EnsureCreated();
}

static bool HasAnyUserTables(DbConnection connection)
{
    using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT 1
        FROM sqlite_master
        WHERE type = 'table'
          AND name NOT LIKE 'sqlite_%'
        LIMIT 1;
        """;

    return command.ExecuteScalar() is not null;
}

static bool RequiresDatabaseReset(DbConnection connection)
{
    var legacyTables = new[]
    {
        "Courses",
        "CourseInstructors",
        "CourseScenarios",
        "Enrollments"
    };

    if (legacyTables.Any(table => TableExists(connection, table)))
    {
        return true;
    }

    var requiredTables = new[]
    {
        "Users",
        "Classes",
        "ClassTeachers",
        "ClassStudents",
        "Scenarios",
        "Assignments",
        "Submissions"
    };

    if (requiredTables.Any(table => !TableExists(connection, table)))
    {
        return true;
    }

    return TableExists(connection, "Users") && !GetTableColumns(connection, "Users").Contains("CreatedAtUtc")
        || TableExists(connection, "Scenarios") && !GetTableColumns(connection, "Scenarios").Contains("CreatedAtUtc")
        || TableExists(connection, "Scenarios") && !GetTableColumns(connection, "Scenarios").Contains("CreatedByEmail")
        || TableExists(connection, "Classes") && !GetTableColumns(connection, "Classes").Contains("CreatedAtUtc")
        || TableExists(connection, "Submissions") && !GetTableColumns(connection, "Submissions").Contains("AssignmentId");
}

static void BackupLegacyDatabaseFiles(string dbPath, ILogger logger)
{
    var directory = Path.GetDirectoryName(dbPath);
    var fileName = Path.GetFileNameWithoutExtension(dbPath);
    var extension = Path.GetExtension(dbPath);
    var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");

    if (directory is null)
    {
        throw new InvalidOperationException($"Could not determine directory for database path '{dbPath}'.");
    }

    Directory.CreateDirectory(directory);

    foreach (var sourcePath in new[] { dbPath, $"{dbPath}-shm", $"{dbPath}-wal" })
    {
        if (!File.Exists(sourcePath))
        {
            continue;
        }

        var sourceFileName = Path.GetFileName(sourcePath);
        var suffix = sourceFileName[(fileName.Length + extension.Length)..];
        var backupPath = Path.Combine(directory, $"{fileName}.legacy-{timestamp}{extension}{suffix}");
        File.Move(sourcePath, backupPath);
        logger.LogWarning("Moved legacy database file '{Source}' to '{Backup}'.", sourceFileName, Path.GetFileName(backupPath));
    }
}

static bool TableExists(DbConnection connection, string tableName)
{
    using var command = connection.CreateCommand();
    command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";

    var parameter = command.CreateParameter();
    parameter.ParameterName = "$name";
    parameter.Value = tableName;
    command.Parameters.Add(parameter);

    return command.ExecuteScalar() is not null;
}

static HashSet<string> GetTableColumns(DbConnection connection, string tableName)
{
    using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info(\"{tableName}\");";

    using var reader = command.ExecuteReader();
    var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    while (reader.Read())
    {
        columns.Add(reader.GetString(1));
    }

    return columns;
}

static bool HasUniqueClassScenarioConstraint(DbConnection connection)
{
    var uniqueIndexes = new List<string>();

    using (var command = connection.CreateCommand())
    {
        command.CommandText = "PRAGMA index_list(\"Assignments\");";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var isUnique = reader.GetInt32(2) == 1;
            if (!isUnique)
            {
                continue;
            }

            uniqueIndexes.Add(reader.GetString(1));
        }
    }

    foreach (var indexName in uniqueIndexes)
    {
        var indexColumns = new List<string>();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA index_info(\"{indexName}\");";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            indexColumns.Add(reader.GetString(2));
        }

        if (indexColumns.Count == 2
            && indexColumns.Contains("ClassId", StringComparer.OrdinalIgnoreCase)
            && indexColumns.Contains("ScenarioId", StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static void RebuildAssignmentsTable(DbConnection connection, bool sourceHasAssignedAtUtc)
{
    var assignedAtSelect = sourceHasAssignedAtUtc
        ? "COALESCE(AssignedAtUtc, CURRENT_TIMESTAMP)"
        : "CURRENT_TIMESTAMP";

    using var command = connection.CreateCommand();
    command.CommandText =
        $"""
        PRAGMA foreign_keys = OFF;
        BEGIN TRANSACTION;
        DROP TABLE IF EXISTS Assignments_new;
        CREATE TABLE Assignments_new (
            Id INTEGER NOT NULL CONSTRAINT PK_Assignments PRIMARY KEY AUTOINCREMENT,
            ClassId INTEGER NOT NULL,
            ScenarioId TEXT NOT NULL,
            Name TEXT NOT NULL,
            AssignedAtUtc TEXT NOT NULL,
            DueAtUtc TEXT NULL,
            AssignedByUserId INTEGER NOT NULL,
            CONSTRAINT FK_Assignments_Classes_ClassId FOREIGN KEY (ClassId) REFERENCES Classes (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_Assignments_Scenarios_ScenarioId FOREIGN KEY (ScenarioId) REFERENCES Scenarios (Id) ON DELETE NO ACTION,
            CONSTRAINT FK_Assignments_Users_AssignedByUserId FOREIGN KEY (AssignedByUserId) REFERENCES Users (Id) ON DELETE RESTRICT
        );
        INSERT INTO Assignments_new (Id, ClassId, ScenarioId, Name, AssignedAtUtc, DueAtUtc, AssignedByUserId)
        SELECT Id, ClassId, ScenarioId, Name, {assignedAtSelect}, DueAtUtc, AssignedByUserId
        FROM Assignments;
        DROP TABLE Assignments;
        ALTER TABLE Assignments_new RENAME TO Assignments;
        CREATE INDEX IF NOT EXISTS IX_Assignments_AssignedByUserId ON Assignments (AssignedByUserId);
        CREATE INDEX IF NOT EXISTS IX_Assignments_ClassId ON Assignments (ClassId);
        CREATE INDEX IF NOT EXISTS IX_Assignments_ScenarioId ON Assignments (ScenarioId);
        COMMIT;
        PRAGMA foreign_keys = ON;
        """;
    command.ExecuteNonQuery();
}
