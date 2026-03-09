namespace PracticeBeforeThePatient.Data.Entities;

public class CourseScenarioEntity
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public string ScenarioId { get; set; } = "";

    public CourseEntity Course { get; set; } = null!;
    public ScenarioEntity Scenario { get; set; } = null!;
}
