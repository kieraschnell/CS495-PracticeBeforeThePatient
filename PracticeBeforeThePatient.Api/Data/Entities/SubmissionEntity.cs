namespace PracticeBeforeThePatient.Data.Entities;

public class SubmissionEntity
{
    public int Id { get; set; }
    public int StudentId { get; set; }
    public string ScenarioId { get; set; } = "";
    public int CourseId { get; set; }
    public string AnswersJson { get; set; } = "{}";
    public decimal? Grade { get; set; }

    public UserEntity Student { get; set; } = null!;
    public ScenarioEntity Scenario { get; set; } = null!;
    public CourseEntity Course { get; set; } = null!;
}
