using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Data.Entities;

namespace PracticeBeforeThePatient.Tests.Fixtures;

public static class TestDataSeeder
{
    public static UserEntity CreateAdmin(AppDbContext db, string email = "admin@ua.edu")
    {
        var user = new UserEntity
        {
            Email = email,
            Name = "Test Admin",
            Role = "admin",
            SsoSubject = Guid.NewGuid().ToString()
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static UserEntity CreateTeacher(AppDbContext db, string email = "teacher@ua.edu")
    {
        var user = new UserEntity
        {
            Email = email,
            Name = "Test Teacher",
            Role = "teacher",
            SsoSubject = Guid.NewGuid().ToString()
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static UserEntity CreateStudent(AppDbContext db, string email = "student@ua.edu")
    {
        var user = new UserEntity
        {
            Email = email,
            Name = "Test Student",
            Role = "student",
            SsoSubject = Guid.NewGuid().ToString()
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static ClassEntity CreateClass(AppDbContext db, UserEntity createdBy, string name = "Test Class")
    {
        var classEntity = new ClassEntity
        {
            Name = name,
            CreatedByUserId = createdBy.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Classes.Add(classEntity);
        db.SaveChanges();
        return classEntity;
    }

    public static void AddTeacherToClass(AppDbContext db, ClassEntity classEntity, UserEntity teacher, UserEntity addedBy)
    {
        db.ClassTeachers.Add(new ClassTeacherEntity
        {
            ClassId = classEntity.Id,
            TeacherUserId = teacher.Id,
            AddedByUserId = addedBy.Id,
            AddedAtUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    public static void AddStudentToClass(AppDbContext db, ClassEntity classEntity, UserEntity student, UserEntity addedBy)
    {
        db.ClassStudents.Add(new ClassStudentEntity
        {
            ClassId = classEntity.Id,
            StudentUserId = student.Id,
            AddedByUserId = addedBy.Id,
            AddedAtUtc = DateTime.UtcNow
        });
        db.SaveChanges();
    }

    public static ScenarioEntity CreateScenario(AppDbContext db, string id = "test-scenario", string title = "Test Scenario")
    {
        var scenario = new ScenarioEntity
        {
            Id = id,
            Title = title,
            Description = "Test scenario description",
            CreatedByEmail = "admin@ua.edu",
            NodesJson = "{}",
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Scenarios.Add(scenario);
        db.SaveChanges();
        return scenario;
    }

    public static AssignmentEntity CreateAssignment(
        AppDbContext db,
        ClassEntity classEntity,
        ScenarioEntity scenario,
        UserEntity assignedBy,
        string name = "Test Assignment",
        DateTime? dueAt = null)
    {
        var assignment = new AssignmentEntity
        {
            ClassId = classEntity.Id,
            ScenarioId = scenario.Id,
            Name = name,
            AssignedByUserId = assignedBy.Id,
            AssignedAtUtc = DateTime.UtcNow.AddDays(-1),
            DueAtUtc = dueAt ?? DateTime.UtcNow.AddDays(7)
        };
        db.Assignments.Add(assignment);
        db.SaveChanges();
        return assignment;
    }

    public static SubmissionEntity CreateSubmission(
        AppDbContext db,
        AssignmentEntity assignment,
        UserEntity student,
        string submissionText = "Test submission")
    {
        var submission = new SubmissionEntity
        {
            AssignmentId = assignment.Id,
            StudentUserId = student.Id,
            SubmissionText = submissionText,
            SubmittedAtUtc = DateTime.UtcNow
        };
        db.Submissions.Add(submission);
        db.SaveChanges();
        return submission;
    }
}
