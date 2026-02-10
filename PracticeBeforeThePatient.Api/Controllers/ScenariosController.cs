using Microsoft.AspNetCore.Mvc;
using PracticeBeforeThePatient.Core.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private static readonly Regex ScenarioIdPattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);
    private readonly string _dataPath;

    public ScenariosController(IWebHostEnvironment env)
    {
        _dataPath = Path.Combine(env.ContentRootPath, "Data", "scenarios");
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
    public ActionResult<List<string>> GetAvailableScenarios()
    {
        try
        {
            if (!Directory.Exists(_dataPath))
            {
                return Ok(new List<string>());
            }

            var scenarios = Directory.GetFiles(_dataPath, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .ToList();

            return Ok(scenarios!);
        }
        catch (Exception ex)
        {
            return StatusCode(500, Problem(
                title: "Error retrieving scenarios",
                detail: ex.Message
            ));
        }
    }
}
