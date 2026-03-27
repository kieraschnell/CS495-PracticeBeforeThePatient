using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Data.Entities;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/assignments")]
public sealed class AssignmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DevAccessStore _access;

    public AssignmentsController(AppDbContext db, DevAccessStore access)
    {
        _db = db;
        _access = access;
    }

    public sealed class SubmitScenarioRequest
    {
        public string ScenarioId { get; set; } = "";
        public string SubmissionText { get; set; } = "";
    }

    public sealed class SubmitScenarioResponse
    {
        public int UpdatedAssignments { get; set; }
    }

    [HttpPost("submit-scenario")]
    public async Task<ActionResult<SubmitScenarioResponse>> SubmitScenario([FromBody] SubmitScenarioRequest? req)
    {
        if (req is null || string.IsNullOrWhiteSpace(req.ScenarioId))
        {
            return BadRequest("Scenario id is required.");
        }

        var studentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(studentEmail))
        {
            return BadRequest("Current user email is not set.");
        }

        var student = await _db.Users.FirstOrDefaultAsync(u => u.Email == studentEmail);
        if (student is null)
        {
            return BadRequest("Current user not found.");
        }

        var scenarioId = req.ScenarioId.Trim();
        var nowUtc = DateTime.UtcNow;

        var enrolledClassIds = await _db.ClassStudents
            .Where(cs => cs.StudentUserId == student.Id)
            .Select(cs => cs.ClassId)
            .ToListAsync();

        var matchingAssignments = await _db.Assignments
            .Where(a => enrolledClassIds.Contains(a.ClassId)
                && a.ScenarioId == scenarioId
                && a.AssignedAtUtc <= nowUtc
                && (!a.DueAtUtc.HasValue || a.DueAtUtc.Value >= nowUtc))
            .Include(a => a.Submissions.Where(s => s.StudentUserId == student.Id))
            .ToListAsync();

        var touched = 0;
        foreach (var assignment in matchingAssignments)
        {
            var submission = assignment.Submissions.FirstOrDefault();
            if (submission is null)
            {
                submission = new SubmissionEntity
                {
                    AssignmentId = assignment.Id,
                    StudentUserId = student.Id
                };
                _db.Submissions.Add(submission);
            }

            submission.SubmittedAtUtc = nowUtc;
            submission.SubmissionText = (req.SubmissionText ?? "").Trim();
            touched++;
        }

        if (touched > 0)
        {
            await _db.SaveChangesAsync();
        }

        return new SubmitScenarioResponse
        {
            UpdatedAssignments = touched
        };
    }
}
