namespace PracticeBeforeThePatient.Data.Entities;

public class ScenarioEntity
{
    public string Id { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string NodesJson { get; set; } = "{}";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
