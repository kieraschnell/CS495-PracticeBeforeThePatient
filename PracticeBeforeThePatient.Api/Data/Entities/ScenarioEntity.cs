namespace PracticeBeforeThePatient.Data.Entities;

public class ScenarioEntity
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string CreatedByEmail { get; set; } = "";
    public string NodesJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<AssignmentEntity> Assignments { get; set; } = [];
}
