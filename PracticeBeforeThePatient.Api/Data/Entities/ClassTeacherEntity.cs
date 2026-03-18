namespace PracticeBeforeThePatient.Data.Entities;

public class ClassTeacherEntity
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public int TeacherUserId { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
    public int AddedByUserId { get; set; }

    public ClassEntity Class { get; set; } = null!;
    public UserEntity Teacher { get; set; } = null!;
    public UserEntity AddedBy { get; set; } = null!;
}
