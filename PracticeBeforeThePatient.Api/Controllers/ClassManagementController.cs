using Microsoft.AspNetCore.Mvc;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/classes")]
public sealed class ClassesController : ControllerBase
{
    private readonly ClassRosterStore _store;

    public ClassesController(ClassRosterStore store)
    {
        _store = store;
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
        var all = await _store.GetAllAsync();

        var sorted = all
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ClassRosterDto
            {
                Id = x.Id,
                Name = x.Name,
                Students = x.Students
            })
            .ToList();

        return sorted;
    }


    public sealed class CreateClassRequest
    {
        public string Name { get; set; } = "";
    }

    [HttpPost]
    public async Task<ActionResult<ClassRosterDto>> Create([FromBody] CreateClassRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest("Class name cannot be empty.");

        var created = await _store.CreateClassAsync(req.Name);

        if (created == null)
            return Conflict("A class with that name already exists.");

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
        var ok = await _store.AddStudentAsync(classId, req.Email ?? "");
        if (!ok) return BadRequest();
        return NoContent();
    }

    [HttpDelete("{classId}/students")]
    public async Task<IActionResult> RemoveStudent(string classId, [FromBody] StudentRequest req)
    {
        await _store.RemoveStudentAsync(classId, req.Email ?? "");
        return NoContent();
    }

    [HttpDelete("{classId}")]
    public async Task<IActionResult> Delete(string classId)
    {
        await _store.DeleteClassAsync(classId);
        return NoContent();
    }
}
