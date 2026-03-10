using Microsoft.AspNetCore.Mvc;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/classes")]
public sealed class ClassesController : ControllerBase
{
    private readonly ClassRosterStore _store;
    private readonly DevAccessStore _access;

    public ClassesController(ClassRosterStore store, DevAccessStore access)
    {
        _store = store;
        _access = access;
    }

    private async Task<bool> RequireTeacher()
    {
        return await _access.IsTeacherAsync();
    }

    public sealed class ClassRosterDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> Students { get; set; } = new();
    }

    [HttpGet]
    public async Task<ActionResult<List<ClassRosterDto>>> GetAll()
    {
        if (!await RequireTeacher()) return Forbid();

        var all = await _store.GetAllAsync();

        return all
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ClassRosterDto
            {
                Id = x.Id,
                Name = x.Name,
                Students = x.Students
            })
            .ToList();
    }

    public sealed class CreateClassRequest
    {
        public string Name { get; set; } = "";
    }

    [HttpPost]
    public async Task<ActionResult<ClassRosterDto>> Create([FromBody] CreateClassRequest req)
    {
        if (!await RequireTeacher()) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest("Class name cannot be empty.");
        }

        var created = await _store.CreateClassAsync(req.Name);

        if (created == null)
        {
            return Conflict("A class with that name already exists.");
        }

        return new ClassRosterDto
        {
            Id = created.Id,
            Name = created.Name,
            Students = created.Students
        };
    }

    public sealed class StudentRequest
    {
        public string Email { get; set; } = "";
    }

    [HttpPost("{classId}/students")]
    public async Task<IActionResult> AddStudent(string classId, [FromBody] StudentRequest req)
    {
        if (!await RequireTeacher()) return Forbid();

        var ok = await _store.AddStudentAsync(classId, req.Email ?? "");
        if (!ok) return BadRequest();
        return NoContent();
    }

    [HttpDelete("{classId}/students")]
    public async Task<IActionResult> RemoveStudent(string classId, [FromBody] StudentRequest req)
    {
        if (!await RequireTeacher()) return Forbid();

        await _store.RemoveStudentAsync(classId, req.Email ?? "");
        return NoContent();
    }

    [HttpDelete("{classId}")]
    public async Task<IActionResult> Delete(string classId)
    {
        if (!await RequireTeacher()) return Forbid();

        await _store.DeleteClassAsync(classId);
        return NoContent();
    }

    public sealed class AssignmentSubmissionDto
    {
        public string StudentEmail { get; set; } = "";
        public DateTimeOffset? SubmittedAtUtc { get; set; }
        public string SubmissionText { get; set; } = "";
        public decimal? Grade { get; set; }
        public string GradeFeedback { get; set; } = "";
        public DateTimeOffset? GradedAtUtc { get; set; }
        public string GradedByEmail { get; set; } = "";
    }

    public sealed class AssignmentDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public DateTimeOffset AssignedAtUtc { get; set; }
        public DateTimeOffset? DueAtUtc { get; set; }
        public List<AssignmentSubmissionDto> Submissions { get; set; } = new();
    }

    [HttpGet("{classId}/assignments")]
    public async Task<ActionResult<List<AssignmentDto>>> GetAssignments(string classId)
    {
        if (!await RequireTeacher()) return Forbid();

        var assignments = await _store.GetAssignmentsAsync(classId);
        if (assignments is null) return NotFound();

        return assignments
            .OrderBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.ScenarioId, StringComparer.OrdinalIgnoreCase)
            .Select(a => new AssignmentDto
            {
                Id = a.Id,
                Name = a.Name,
                ScenarioId = a.ScenarioId,
                AssignedAtUtc = a.AssignedAtUtc,
                DueAtUtc = a.DueAtUtc,
                Submissions = a.Submissions
                    .OrderBy(x => x.StudentEmail, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new AssignmentSubmissionDto
                    {
                        StudentEmail = s.StudentEmail,
                        SubmittedAtUtc = s.SubmittedAtUtc,
                        SubmissionText = s.SubmissionText,
                        Grade = s.Grade,
                        GradeFeedback = s.GradeFeedback,
                        GradedAtUtc = s.GradedAtUtc,
                        GradedByEmail = s.GradedByEmail
                    })
                    .ToList()
            })
            .ToList();
    }

    public sealed class CreateAssignmentRequest
    {
        public string Name { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public DateTimeOffset? DueAtUtc { get; set; }
    }

    [HttpPost("{classId}/assignments")]
    public async Task<ActionResult<AssignmentDto>> CreateAssignment(string classId, [FromBody] CreateAssignmentRequest req)
    {
        if (!await RequireTeacher()) return Forbid();

        if (string.IsNullOrWhiteSpace(req.Name))
        {
            return BadRequest("Assignment name is required.");
        }

        if (string.IsNullOrWhiteSpace(req.ScenarioId))
        {
            return BadRequest("Scenario id is required.");
        }

        var created = await _store.CreateAssignmentAsync(classId, req.Name, req.ScenarioId, req.DueAtUtc);
        if (created is null) return BadRequest("Invalid class id or scenario id.");

        return new AssignmentDto
        {
            Id = created.Id,
            Name = created.Name,
            ScenarioId = created.ScenarioId,
            AssignedAtUtc = created.AssignedAtUtc,
            DueAtUtc = created.DueAtUtc,
            Submissions = new List<AssignmentSubmissionDto>()
        };
    }

    [HttpDelete("{classId}/assignments/{assignmentId}")]
    public async Task<IActionResult> DeleteAssignment(string classId, string assignmentId)
    {
        if (!await RequireTeacher()) return Forbid();

        var ok = await _store.DeleteAssignmentAsync(classId, assignmentId);
        if (!ok) return NotFound();

        return NoContent();
    }

    public sealed class UpdateAssignmentDueRequest
    {
        public DateTimeOffset? DueAtUtc { get; set; }
    }

    [HttpPut("{classId}/assignments/{assignmentId}/due")]
    public async Task<IActionResult> UpdateAssignmentDue(string classId, string assignmentId, [FromBody] UpdateAssignmentDueRequest req)
    {
        if (!await RequireTeacher()) return Forbid();

        var ok = await _store.UpdateAssignmentDueAtAsync(classId, assignmentId, req.DueAtUtc);
        if (!ok) return NotFound();

        return NoContent();
    }

    public sealed class GradeAssignmentRequest
    {
        public string StudentEmail { get; set; } = "";
        public decimal? Grade { get; set; }
        public string Feedback { get; set; } = "";
    }

    [HttpPut("{classId}/assignments/{assignmentId}/grades")]
    public async Task<IActionResult> GradeAssignment(string classId, string assignmentId, [FromBody] GradeAssignmentRequest req)
    {
        if (!await RequireTeacher()) return Forbid();

        if (string.IsNullOrWhiteSpace(req.StudentEmail))
        {
            return BadRequest("Student email is required.");
        }

        if (req.Grade is < 0 or > 100)
        {
            return BadRequest("Grade must be between 0 and 100.");
        }

        var grader = await _access.GetCurrentEmailAsync();
        var ok = await _store.GradeAssignmentAsync(
            classId,
            assignmentId,
            req.StudentEmail,
            req.Grade,
            req.Feedback ?? "",
            grader);

        if (!ok) return BadRequest("Invalid class, assignment, or student.");
        return NoContent();
    }

    public sealed class StudentGradeDto
    {
        public string ClassId { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string AssignmentId { get; set; } = "";
        public string AssignmentName { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public DateTimeOffset AssignedAtUtc { get; set; }
        public DateTimeOffset? DueAtUtc { get; set; }
        public DateTimeOffset? SubmittedAtUtc { get; set; }
        public string SubmissionText { get; set; } = "";
        public decimal? Grade { get; set; }
        public string GradeFeedback { get; set; } = "";
        public DateTimeOffset? GradedAtUtc { get; set; }
        public string GradedByEmail { get; set; } = "";
    }

    [HttpGet("me/grades")]
    public async Task<ActionResult<List<StudentGradeDto>>> GetMyGrades()
    {
        var email = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Ok(new List<StudentGradeDto>());
        }

        var all = await _store.GetAllAsync();

        var grades = all
            .Where(c => c.Students.Any(s => string.Equals(s, email, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(c => (c.Assignments ?? new List<ClassRosterStore.ClassAssignment>())
                .Select(a =>
                {
                    var submission = (a.Submissions ?? new List<ClassRosterStore.AssignmentSubmission>())
                        .FirstOrDefault(s => string.Equals(s.StudentEmail, email, StringComparison.OrdinalIgnoreCase));

                    return new StudentGradeDto
                    {
                        ClassId = c.Id,
                        ClassName = c.Name,
                        AssignmentId = a.Id,
                        AssignmentName = a.Name,
                        ScenarioId = a.ScenarioId,
                        AssignedAtUtc = a.AssignedAtUtc,
                        DueAtUtc = a.DueAtUtc,
                        SubmittedAtUtc = submission?.SubmittedAtUtc,
                        SubmissionText = submission?.SubmissionText ?? "",
                        Grade = submission?.Grade,
                        GradeFeedback = submission?.GradeFeedback ?? "",
                        GradedAtUtc = submission?.GradedAtUtc,
                        GradedByEmail = submission?.GradedByEmail ?? ""
                    };
                }))
            .OrderBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.AssignmentName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return grades;
    }
}
