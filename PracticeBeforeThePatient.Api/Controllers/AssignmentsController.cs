using Microsoft.AspNetCore.Mvc;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/assignments")]
public sealed class AssignmentsController : ControllerBase
{
    private readonly ClassRosterStore _classes;
    private readonly DevAccessStore _access;

    public AssignmentsController(ClassRosterStore classes, DevAccessStore access)
    {
        _classes = classes;
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

        var studentEmail = await _access.GetCurrentEmailAsync();
        if (string.IsNullOrWhiteSpace(studentEmail))
        {
            return BadRequest("Current user email is not set.");
        }

        var updated = await _classes.SubmitScenarioAsync(studentEmail, req.ScenarioId, req.SubmissionText ?? "");

        return new SubmitScenarioResponse
        {
            UpdatedAssignments = updated
        };
    }
}
