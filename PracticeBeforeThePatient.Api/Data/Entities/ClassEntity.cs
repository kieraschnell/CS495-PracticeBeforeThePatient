namespace PracticeBeforeThePatient.Data.Entities;

public class ClassEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; set; }

    public UserEntity CreatedBy { get; set; } = null!;
    public ICollection<ClassTeacherEntity> Teachers { get; set; } = [];
    public ICollection<ClassStudentEntity> Students { get; set; } = [];
    public ICollection<AssignmentEntity> Assignments { get; set; } = [];
}
