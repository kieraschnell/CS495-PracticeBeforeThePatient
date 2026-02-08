namespace PracticeBeforeThePatient.Core.Models;

public class Scenario
{
    public int Id { get; set; }
    public string Title { get; set; } = "";

    // Foreign key for the root Node
    public int RootNodeId { get; set; }

    // Navigation property to the root Node
    public Node Root { get; set; } = new();
}