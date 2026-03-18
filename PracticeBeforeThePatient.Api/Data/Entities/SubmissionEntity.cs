namespace PracticeBeforeThePatient.Data.Entities;

public class SubmissionEntity
{
    public int Id { get; set; }
    public int AssignmentId { get; set; }
    public int StudentUserId { get; set; }
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public string SubmissionText { get; set; } = "";
    public decimal? Grade { get; set; }
    public string? GradeFeedback { get; set; }
    public DateTime? GradedAtUtc { get; set; }
    public int? GradedByUserId { get; set; }

    public AssignmentEntity Assignment { get; set; } = null!;
    public UserEntity Student { get; set; } = null!;
    public UserEntity? GradedBy { get; set; }
}
