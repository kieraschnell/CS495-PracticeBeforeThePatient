using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Data.Entities;
using PracticeBeforeThePatient.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private static readonly Regex ScenarioIdPattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly AppDbContext _db;
    private readonly DevAccessStore _access;

    public ScenariosController(AppDbContext db, DevAccessStore access)
    {
        _db = db;
        _access = access;
    }

    [HttpGet("{scenarioId}")]
    public async Task<ActionResult<Scenario>> GetScenario(string scenarioId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || !ScenarioIdPattern.IsMatch(scenarioId))
        {
            return BadRequest(Problem(
                title: "Invalid scenario id",
                detail: "Scenario id may only include letters, numbers, underscore, and hyphen."
            ));
        }

        if (!await CanCurrentUserAccessScenarioAsync(scenarioId))
        {
            return Forbid();
        }

        var entity = await _db.Scenarios.AsNoTracking().FirstOrDefaultAsync(s => s.Id == scenarioId, ct);

        if (entity is null)
        {
            return NotFound(Problem(
                title: "Scenario not found",
                detail: $"Scenario '{scenarioId}' not found."
            ));
        }

        return Ok(ToModel(entity));
    }

    [HttpGet]
    public async Task<ActionResult<List<string>>> GetAvailableScenarios()
    {
        var allScenarios = await _db.Scenarios
            .Select(s => s.Id)
            .ToListAsync();

        if (await _access.IsTeacherAsync())
        {
            return Ok(allScenarios);
        }

        var email = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Ok(new List<string>());
        }

        var student = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (student is null)
        {
            return Ok(new List<string>());
        }

        var nowUtc = DateTime.UtcNow;
        var allScenariosSet = allScenarios.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enrolledClassIds = await _db.ClassStudents
            .Where(cs => cs.StudentUserId == student.Id)
            .Select(cs => cs.ClassId)
            .ToListAsync();

        var allowedScenarioIds = await _db.Assignments
            .Where(a => enrolledClassIds.Contains(a.ClassId))
            .Where(a => a.AssignedAtUtc <= nowUtc)
            .Where(a => !a.DueAtUtc.HasValue || a.DueAtUtc.Value >= nowUtc)
            .Select(a => a.ScenarioId)
            .Distinct()
            .ToListAsync();

        var filtered = allowedScenarioIds
            .Where(id => !string.IsNullOrWhiteSpace(id) && allScenariosSet.Contains(id))
            .ToList();

        return Ok(filtered);
    }

    [HttpPut("{scenarioId}")]
    public async Task<IActionResult> UpdateScenario(string scenarioId, [FromBody] Scenario scenario, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(scenarioId) || !ScenarioIdPattern.IsMatch(scenarioId))
        {
            return BadRequest(Problem(
                title: "Invalid scenario id",
                detail: "Scenario id may only include letters, numbers, underscore, and hyphen."
            ));
        }

        if (scenario is null)
        {
            return BadRequest(Problem(
                title: "Invalid scenario data",
                detail: "Scenario cannot be null."
            ));
        }

        if (!await _access.IsTeacherAsync())
        {
            return Forbid();
        }

        try
        {
            _ = JsonSerializer.Serialize(scenario.Root ?? new Node(), JsonOptions);
        }
        catch (Exception)
        {
            return BadRequest(Problem(
                title: "Invalid scenario data",
                detail: "Scenario root node could not be serialized."
            ));
        }

        var entity = await _db.Scenarios.FirstOrDefaultAsync(s => s.Id == scenarioId, ct);

        if (entity is null)
        {
            _db.Scenarios.Add(ToEntity(scenarioId, scenario));
        }
        else
        {
            entity.CreatedByEmail = string.IsNullOrWhiteSpace(scenario.CreatedBy)
                ? entity.CreatedByEmail
                : scenario.CreatedBy;
            entity.Title = scenario.Title ?? scenarioId;
            entity.Description = scenario.Description ?? "";
            if (scenario.CreatedAt != default)
            {
                entity.CreatedAtUtc = scenario.CreatedAt;
            }
            entity.NodesJson = JsonSerializer.Serialize(scenario.Root ?? new Node(), JsonOptions);
        }

        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Scenario saved successfully" });
    }

    private async Task<bool> CanCurrentUserAccessScenarioAsync(string scenarioId)
    {
        if (await _access.IsTeacherAsync())
        {
            return true;
        }

        var email = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var student = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (student is null)
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;
        var enrolledClassIds = await _db.ClassStudents
            .Where(cs => cs.StudentUserId == student.Id)
            .Select(cs => cs.ClassId)
            .ToListAsync();

        return await _db.Assignments
            .AnyAsync(a => enrolledClassIds.Contains(a.ClassId)
                && a.ScenarioId == scenarioId
                && a.AssignedAtUtc <= nowUtc
                && (!a.DueAtUtc.HasValue || a.DueAtUtc.Value >= nowUtc));
    }

    private static Scenario ToModel(ScenarioEntity entity)
    {
        return new Scenario
        {
            Id = entity.Id,
            CreatedBy = entity.CreatedByEmail,
            Title = entity.Title,
            Description = entity.Description,
            CreatedAt = entity.CreatedAtUtc,
            Root = JsonSerializer.Deserialize<Node>(entity.NodesJson ?? "{}", JsonOptions) ?? new()
        };
    }

    private static ScenarioEntity ToEntity(string id, Scenario model)
    {
        return new ScenarioEntity
        {
            Id = id,
            CreatedByEmail = string.IsNullOrWhiteSpace(model.CreatedBy) ? "admin@ua.edu" : model.CreatedBy,
            Title = model.Title ?? id,
            Description = model.Description ?? "",
            NodesJson = JsonSerializer.Serialize(model.Root ?? new Node(), JsonOptions),
            CreatedAtUtc = model.CreatedAt == default ? DateTime.UtcNow : model.CreatedAt
        };
    }
}
