using Microsoft.AspNetCore.Mvc;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Services;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private static readonly Regex ScenarioIdPattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
    private readonly string _dataPath;
    private readonly DevAccessStore _access;
    private readonly ClassRosterStore _classes;

    public ScenariosController(IWebHostEnvironment env, DevAccessStore access, ClassRosterStore classes)
    {
        _dataPath = Path.Combine(env.ContentRootPath, "Data", "scenarios");
        _access = access;
        _classes = classes;
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

        var filePath = Path.Combine(_dataPath, $"{scenarioId}.json");

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound(Problem(
                title: "Scenario not found",
                detail: $"Scenario '{scenarioId}' not found."
            ));
        }

        try
        {
            var json = await System.IO.File.ReadAllTextAsync(filePath, ct);

            var scenario = JsonSerializer.Deserialize<Scenario>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (scenario == null)
            {
                return StatusCode(500, Problem(
                    title: "Scenario load failed",
                    detail: "Scenario deserialized to null. Check the JSON format."
                ));
            }

            return Ok(scenario);
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (JsonException ex)
        {
            return StatusCode(500, Problem(
                title: "Invalid scenario JSON",
                detail: ex.Message
            ));
        }
        catch (Exception ex)
        {
            return StatusCode(500, Problem(
                title: "Error loading scenario",
                detail: ex.Message
            ));
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<string>>> GetAvailableScenarios()
    {
        try
        {
            if (!Directory.Exists(_dataPath))
            {
                return Ok(new List<string>());
            }

            var allScenarios = Directory.GetFiles(_dataPath, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToList()!;

            if (await _access.IsAdminAsync())
            {
                return Ok(allScenarios);
            }

            var email = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                return Ok(new List<string>());
            }

            var classRosters = await _classes.GetAllAsync();
            var allowedScenarioIds = classRosters
                .Where(c => c.Students.Any(s => string.Equals(s, email, StringComparison.OrdinalIgnoreCase)))
                .SelectMany(c => c.Assignments ?? new List<ClassRosterStore.ClassAssignment>())
                .Select(a => (a.ScenarioId ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var filtered = allScenarios
                .Where(id => allowedScenarioIds.Contains(id))
                .ToList();

            return Ok(filtered);
        }
        catch (Exception ex)
        {
            return StatusCode(500, Problem(
                title: "Error retrieving scenarios",
                detail: ex.Message
            ));
        }
    }

    private async Task<bool> CanCurrentUserAccessScenarioAsync(string scenarioId)
    {
        if (await _access.IsAdminAsync())
        {
            return true;
        }

        var email = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var classRosters = await _classes.GetAllAsync();
        return classRosters
            .Where(c => c.Students.Any(s => string.Equals(s, email, StringComparison.OrdinalIgnoreCase)))
            .SelectMany(c => c.Assignments ?? new List<ClassRosterStore.ClassAssignment>())
            .Any(a => string.Equals(a.ScenarioId, scenarioId, StringComparison.OrdinalIgnoreCase));
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

        if (scenario == null)
        {
            return BadRequest(Problem(
                title: "Invalid scenario data",
                detail: "Scenario cannot be null."
            ));
        }

        try
        {
            if (!Directory.Exists(_dataPath))
            {
                Directory.CreateDirectory(_dataPath);
            }

            var filePath = Path.Combine(_dataPath, $"{scenarioId}.json");

            var json = JsonSerializer.Serialize(scenario, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await System.IO.File.WriteAllTextAsync(filePath, json, ct);

            return Ok(new { message = "Scenario saved successfully" });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499);
        }
        catch (Exception ex)
        {
            return StatusCode(500, Problem(
                title: "Error saving scenario",
                detail: ex.Message
            ));
        }
    }
}
