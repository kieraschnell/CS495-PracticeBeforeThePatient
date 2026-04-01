using PracticeBeforeThePatient.Tests.Fixtures;
using Xunit;

namespace PracticeBeforeThePatient.Tests.Integration;

/// <summary>
/// Tests for the grading workflow using in-memory database.
/// </summary>
public class GradingTests
{
    [Fact]
    public void Submission_CanBeGraded()
    {
        // Arrange
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, "grading-admin@ua.edu");
        var teacher = TestDataSeeder.CreateTeacher(db, "grading-teacher@ua.edu");
        var student = TestDataSeeder.CreateStudent(db, "grading-student@ua.edu");
        var classEntity = TestDataSeeder.CreateClass(db, admin, "Grading Test Class");
        TestDataSeeder.AddTeacherToClass(db, classEntity, teacher, admin);
        TestDataSeeder.AddStudentToClass(db, classEntity, student, admin);
        var scenario = TestDataSeeder.CreateScenario(db, "grading-scenario", "Grading Scenario");
        var assignment = TestDataSeeder.CreateAssignment(db, classEntity, scenario, teacher, "Grading Assignment");
        var submission = TestDataSeeder.CreateSubmission(db, assignment, student, "Student reasoning");

        // Act - Grade the submission
        submission.Grade = 85;
        submission.GradeFeedback = "Good work! Consider more detail in your reasoning.";
        submission.GradedByUserId = teacher.Id;
        submission.GradedAtUtc = DateTime.UtcNow;
        db.SaveChanges();

        // Assert
        var gradedSubmission = db.Submissions.First(s => s.Id == submission.Id);
        Assert.Equal(85, gradedSubmission.Grade);
        Assert.Equal("Good work! Consider more detail in your reasoning.", gradedSubmission.GradeFeedback);
        Assert.Equal(teacher.Id, gradedSubmission.GradedByUserId);
        Assert.NotNull(gradedSubmission.GradedAtUtc);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Grade_AcceptsValidRange(int grade)
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, $"range-admin-{grade}@ua.edu");
        var student = TestDataSeeder.CreateStudent(db, $"range-student-{grade}@ua.edu");
        var classEntity = TestDataSeeder.CreateClass(db, admin, $"Range Test Class {grade}");
        TestDataSeeder.AddStudentToClass(db, classEntity, student, admin);
        var scenario = TestDataSeeder.CreateScenario(db, $"range-scenario-{grade}", $"Range Scenario {grade}");
        var assignment = TestDataSeeder.CreateAssignment(db, classEntity, scenario, admin);
        var submission = TestDataSeeder.CreateSubmission(db, assignment, student);

        submission.Grade = grade;
        db.SaveChanges();

        var retrieved = db.Submissions.First(s => s.Id == submission.Id);
        Assert.Equal(grade, retrieved.Grade);
    }

    [Fact]
    public void SubmissionStates_AreDistinguishable()
    {
        using var db = TestDbContextFactory.CreateInMemoryContext();
        var admin = TestDataSeeder.CreateAdmin(db, "states-admin@ua.edu");
        var student1 = TestDataSeeder.CreateStudent(db, "states-student1@ua.edu");
        var student2 = TestDataSeeder.CreateStudent(db, "states-student2@ua.edu");
        var student3 = TestDataSeeder.CreateStudent(db, "states-student3@ua.edu");
        var classEntity = TestDataSeeder.CreateClass(db, admin, "States Test Class");
        TestDataSeeder.AddStudentToClass(db, classEntity, student1, admin);
        TestDataSeeder.AddStudentToClass(db, classEntity, student2, admin);
        TestDataSeeder.AddStudentToClass(db, classEntity, student3, admin);
        var scenario = TestDataSeeder.CreateScenario(db, "states-scenario", "States Scenario");
        var assignment = TestDataSeeder.CreateAssignment(db, classEntity, scenario, admin);

        // Student 1: Not submitted (no submission record)
        // Student 2: Submitted (has submission, no grade)
        var submission2 = TestDataSeeder.CreateSubmission(db, assignment, student2);
        // Student 3: Graded (has submission with grade)
        var submission3 = TestDataSeeder.CreateSubmission(db, assignment, student3);
        submission3.Grade = 90;
        submission3.GradedByUserId = admin.Id;
        submission3.GradedAtUtc = DateTime.UtcNow;
        db.SaveChanges();

        // Assert different states
        var sub1 = db.Submissions.FirstOrDefault(s =>
            s.AssignmentId == assignment.Id && s.StudentUserId == student1.Id);
        var sub2 = db.Submissions.FirstOrDefault(s =>
            s.AssignmentId == assignment.Id && s.StudentUserId == student2.Id);
        var sub3 = db.Submissions.FirstOrDefault(s =>
            s.AssignmentId == assignment.Id && s.StudentUserId == student3.Id);

        // Not submitted
        Assert.Null(sub1);

        // Submitted but not graded
        Assert.NotNull(sub2);
        Assert.Null(sub2.Grade);

        // Graded
        Assert.NotNull(sub3);
        Assert.Equal(90, sub3.Grade);
    }
}
