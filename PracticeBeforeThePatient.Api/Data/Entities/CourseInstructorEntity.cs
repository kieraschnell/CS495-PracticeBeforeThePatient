namespace PracticeBeforeThePatient.Data.Entities;

public class CourseInstructorEntity
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int InstructorId { get; set; }

    public CourseEntity Course { get; set; } = null!;
    public UserEntity Instructor { get; set; } = null!;
}
