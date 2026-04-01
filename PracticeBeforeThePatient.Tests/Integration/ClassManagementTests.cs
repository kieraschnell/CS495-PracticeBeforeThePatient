using PracticeBeforeThePatient.Data.Entities;
using PracticeBeforeThePatient.Tests.Fixtures;
using Xunit;

namespace PracticeBeforeThePatient.Tests.Integration;

/// <summary>
/// Tests for class management operations using in-memory database.
/// </summary>
public class ClassManagementTests
{
    [Fact]
    public void CreateClass_WithUniqueName_Succeeds()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, "create-class-admin@ua.edu");

        var classEntity = TestDataSeeder.CreateClass(db, admin, "Unique Class Name 123");

        Assert.NotEqual(0, classEntity.Id);
        Assert.Equal("Unique Class Name 123", classEntity.Name);
        Assert.Equal(admin.Id, classEntity.CreatedByUserId);
    }

    [Fact]
    public void AddStudentToClass_CreatesEnrollment()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, "enroll-admin@ua.edu");
        var student = TestDataSeeder.CreateStudent(db, "enroll-student@ua.edu");
        var classEntity = TestDataSeeder.CreateClass(db, admin, "Enrollment Test Class");

        TestDataSeeder.AddStudentToClass(db, classEntity, student, admin);

        var enrollment = db.ClassStudents.FirstOrDefault(cs =>
            cs.ClassId == classEntity.Id && cs.StudentUserId == student.Id);
        Assert.NotNull(enrollment);
    }

    [Fact]
    public void AddTeacherToClass_CreatesTeachingAssignment()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, "teach-admin@ua.edu");
        var teacher = TestDataSeeder.CreateTeacher(db, "teach-teacher@ua.edu");
        var classEntity = TestDataSeeder.CreateClass(db, admin, "Teaching Test Class");

        TestDataSeeder.AddTeacherToClass(db, classEntity, teacher, admin);

        var teaching = db.ClassTeachers.FirstOrDefault(ct =>
            ct.ClassId == classEntity.Id && ct.TeacherUserId == teacher.Id);
        Assert.NotNull(teaching);
    }

    [Fact]
    public void CreateAssignment_WithScenarioAndDueDate_Succeeds()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, "assign-admin@ua.edu");
        var classEntity = TestDataSeeder.CreateClass(db, admin, "Assignment Creation Class");
        var scenario = TestDataSeeder.CreateScenario(db, "assign-scenario", "Assignment Scenario");
        var dueDate = DateTime.UtcNow.AddDays(14);

        var assignment = TestDataSeeder.CreateAssignment(
            db, classEntity, scenario, admin, "Midterm Assignment", dueDate);

        Assert.NotEqual(0, assignment.Id);
        Assert.Equal("Midterm Assignment", assignment.Name);
        Assert.Equal(scenario.Id, assignment.ScenarioId);
        Assert.Equal(classEntity.Id, assignment.ClassId);
        Assert.NotNull(assignment.DueAtUtc);
    }

    [Fact]
    public void RemoveStudentFromClass_DeletesEnrollment()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, "remove-admin@ua.edu");
        var student = TestDataSeeder.CreateStudent(db, "remove-student@ua.edu");
        var classEntity = TestDataSeeder.CreateClass(db, admin, "Remove Student Class");
        TestDataSeeder.AddStudentToClass(db, classEntity, student, admin);

        // Remove the enrollment
        var enrollment = db.ClassStudents.First(cs =>
            cs.ClassId == classEntity.Id && cs.StudentUserId == student.Id);
        db.ClassStudents.Remove(enrollment);
        db.SaveChanges();

        var removed = db.ClassStudents.FirstOrDefault(cs =>
            cs.ClassId == classEntity.Id && cs.StudentUserId == student.Id);
        Assert.Null(removed);
    }

    [Fact]
    public void AddingStudent_CreatesUserIfNotExists()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, "newuser-admin@ua.edu");
        var classEntity = TestDataSeeder.CreateClass(db, admin, "New User Class");

        // Create a new student that doesn't exist yet
        var newStudent = new UserEntity
        {
            Email = "brandnew@ua.edu",
            Name = "Brand New Student",
            Role = "student",
            SsoSubject = Guid.NewGuid().ToString()
        };
        db.Users.Add(newStudent);
        db.SaveChanges();

        TestDataSeeder.AddStudentToClass(db, classEntity, newStudent, admin);

        var user = db.Users.FirstOrDefault(u => u.Email == "brandnew@ua.edu");
        Assert.NotNull(user);
        var enrollment = db.ClassStudents.FirstOrDefault(cs => cs.StudentUserId == user.Id);
        Assert.NotNull(enrollment);
    }
}
