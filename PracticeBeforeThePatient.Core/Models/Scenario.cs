namespace PracticeBeforeThePatient.Core.Models;

public class Scenario
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public Node Root { get; set; } = new();
}
