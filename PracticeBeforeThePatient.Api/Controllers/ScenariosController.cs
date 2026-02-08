using Microsoft.AspNetCore.Mvc;
using PracticeBeforeThePatient.Api.Data;
using PracticeBeforeThePatient.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ScenariosController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ScenariosController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("{scenarioId}")]
    public async Task<ActionResult<Scenario>> GetScenario(int scenarioId)
    {
        try
        {
            var scenario = await _context.Scenarios
                .Include(s => s.Root)
                    .ThenInclude(n => n.Choices)
                        .ThenInclude(c => c.Next)
                .FirstOrDefaultAsync(s => s.Id == scenarioId);

            if (scenario == null)
            {
                return NotFound($"Scenario '{scenarioId}' not found.");
            }

            return Ok(scenario);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error loading scenario: {ex.Message}");
        }
    }

    [HttpGet]
    public async Task<ActionResult<List<Scenario>>> GetAvailableScenarios()
    {
        try
        {
            var scenarios = await _context.Scenarios.ToListAsync();
            return Ok(scenarios);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error retrieving scenarios: {ex.Message}");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Scenario>> CreateScenario(Scenario scenario)
    {
        try
        {
            _context.Scenarios.Add(scenario);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetScenario), new { scenarioId = scenario.Id }, scenario);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error creating scenario: {ex.Message}");
        }
    }
}