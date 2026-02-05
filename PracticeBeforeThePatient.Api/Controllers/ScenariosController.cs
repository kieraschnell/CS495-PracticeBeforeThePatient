using Microsoft.AspNetCore.Mvc;
using PracticeBeforeThePatient.Core.Models;
using System.Text.Json;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private readonly string _dataPath;

    public ScenariosController(IWebHostEnvironment env)
    {
        _dataPath = Path.Combine(env.ContentRootPath, "Data", "scenarios");
    }

    [HttpGet("{scenarioId}")]
    public async Task<ActionResult<Scenario>> GetScenario(string scenarioId)
    {
        try
        {
            var filePath = Path.Combine(_dataPath, $"{scenarioId}.json");

            if (!System.IO.File.Exists(filePath))
            {
                return NotFound($"Scenario '{scenarioId}' not found.");
            }

            var json = await System.IO.File.ReadAllTextAsync(filePath);
            var scenario = JsonSerializer.Deserialize<Scenario>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return Ok(scenario);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error loading scenario: {ex.Message}");
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
                .ToList();

            return Ok(scenarios);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving scenarios: {ex.Message}");
        }
    }
}