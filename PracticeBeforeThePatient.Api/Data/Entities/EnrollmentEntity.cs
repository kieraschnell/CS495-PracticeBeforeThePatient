namespace PracticeBeforeThePatient.Data.Entities;

public class EnrollmentEntity
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int StudentId { get; set; }

    public CourseEntity Course { get; set; } = null!;
    public UserEntity Student { get; set; } = null!;
}
