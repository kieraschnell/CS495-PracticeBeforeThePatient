namespace PracticeBeforeThePatient.Data.Entities;

public class AssignmentEntity
{
    public int Id { get; set; }
    public int ClassId { get; set; }
    public string ScenarioId { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime AssignedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DueAtUtc { get; set; }
    public int AssignedByUserId { get; set; }

    public ClassEntity Class { get; set; } = null!;
    public ScenarioEntity Scenario { get; set; } = null!;
    public UserEntity AssignedBy { get; set; } = null!;
    public ICollection<SubmissionEntity> Submissions { get; set; } = [];
}
