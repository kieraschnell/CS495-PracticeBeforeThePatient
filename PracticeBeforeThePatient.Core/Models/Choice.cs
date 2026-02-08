namespace PracticeBeforeThePatient.Core.Models;

public class Choice
{
    public int Id { get; set; }

    // Foreign key to the parent Node
    public int NodeId { get; set; }

    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
    public string? Feedback { get; set; }

    // Foreign key for the next Node (nullable - not all choices lead to another node)
    public int? NextNodeId { get; set; }

    // Navigation property to the next Node
    public Node? Next { get; set; }
}