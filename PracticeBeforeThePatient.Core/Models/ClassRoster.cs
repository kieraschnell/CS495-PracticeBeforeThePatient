namespace PracticeBeforeThePatient.Core.Models;

public sealed class ClassRoster
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public List<string> Students { get; set; } = new();
}
