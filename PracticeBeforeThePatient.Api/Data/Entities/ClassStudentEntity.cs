namespace PracticeBeforeThePatient.Data.Entities;

public class ClassStudentEntity
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int StudentUserId { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public int AddedByUserId { get; set; }

    public ClassEntity Class { get; set; } = null!;
    public UserEntity Student { get; set; } = null!;
    public UserEntity AddedBy { get; set; } = null!;
}
