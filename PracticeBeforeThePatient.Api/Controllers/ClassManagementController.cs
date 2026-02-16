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

    private async Task<bool> RequireAdmin()
    {
        return await _access.IsAdminAsync();
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
        if (!await RequireAdmin()) return Forbid();

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
        if (!await RequireAdmin()) return Forbid();

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
        if (!await RequireAdmin()) return Forbid();

        var ok = await _store.AddStudentAsync(classId, req.Email ?? "");
        if (!ok) return BadRequest();
        return NoContent();
    }

    [HttpDelete("{classId}/students")]
    public async Task<IActionResult> RemoveStudent(string classId, [FromBody] StudentRequest req)
    {
        if (!await RequireAdmin()) return Forbid();

        await _store.RemoveStudentAsync(classId, req.Email ?? "");
        return NoContent();
    }

    [HttpDelete("{classId}")]
    public async Task<IActionResult> Delete(string classId)
    {
        if (!await RequireAdmin()) return Forbid();

        await _store.DeleteClassAsync(classId);
        return NoContent();
    }

    [HttpGet("{classId}/scenarios")]
    public async Task<ActionResult<List<string>>> GetScenarioAccess(string classId)
    {
        if (!await RequireAdmin()) return Forbid();

        var allowed = await _store.GetAllowedScenarioIdsAsync(classId);
        if (allowed is null) return NotFound();
        return allowed;
    }

    public sealed class SetScenarioAccessRequest
    {
        public List<string> ScenarioIds { get; set; } = new();
    }

    [HttpPut("{classId}/scenarios")]
    public async Task<IActionResult> SetScenarioAccess(string classId, [FromBody] SetScenarioAccessRequest req)
    {
        if (!await RequireAdmin()) return Forbid();

        var ok = await _store.SetAllowedScenarioIdsAsync(classId, req.ScenarioIds ?? new List<string>());
        if (!ok) return BadRequest("Invalid class id or scenario ids.");
        return NoContent();
    }
}
