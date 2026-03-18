namespace PracticeBeforeThePatient.Data.Entities;

public class UserEntity
{
    public int Id { get; set; }
    public string SsoSubject { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "student";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ClassEntity> CreatedClasses { get; set; } = [];
    public ICollection<ClassTeacherEntity> TeachingAssignments { get; set; } = [];
    public ICollection<ClassStudentEntity> StudentEnrollments { get; set; } = [];
    public ICollection<AssignmentEntity> AssignedAssignments { get; set; } = [];
    public ICollection<SubmissionEntity> SubmittedSubmissions { get; set; } = [];
    public ICollection<SubmissionEntity> GradedSubmissions { get; set; } = [];
}
