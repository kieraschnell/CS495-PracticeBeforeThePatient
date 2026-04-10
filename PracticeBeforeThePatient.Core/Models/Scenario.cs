namespace PracticeBeforeThePatient.Core.Models;

public class Scenario
{
    public string Id { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public Node Root { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
