using Microsoft.AspNetCore.Mvc;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AccessController : ControllerBase
{
    private readonly DevAccessStore _devAccess;
    private readonly ClassRosterStore _classes;
    private readonly IWebHostEnvironment _env;

    public AccessController(DevAccessStore devAccess, ClassRosterStore classes, IWebHostEnvironment env)
    {
        _devAccess = devAccess;
        _classes = classes;
        _env = env;
    }

    public sealed class AccessResponse
    {
        public string Email { get; set; } = "";
        public bool IsAdmin { get; set; }
        public List<string> AllowedScenarioIds { get; set; } = new();
        public List<AllowedScenarioOptionDto> AllowedScenarioOptions { get; set; } = new();
        public string Theme { get; set; } = DevAccessStore.LightTheme;
    }

    public sealed class AllowedScenarioOptionDto
    {
        public string AssignmentId { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public string Label { get; set; } = "";
        public DateTimeOffset? DueAtUtc { get; set; }
        public bool IsSubmitted { get; set; }
    }

    public sealed class SetDevUserRequest
    {
        public string Email { get; set; } = "";
    }

    public sealed class SetThemeRequest
    {
        public string Theme { get; set; } = DevAccessStore.LightTheme;
    }

    [HttpGet]
    public async Task<ActionResult<AccessResponse>> Get()
    {
        var response = await BuildAccessResponseAsync();
        return response;
    }

    [HttpPost("dev-user")]
    public async Task<ActionResult<AccessResponse>> SetDevUser([FromBody] SetDevUserRequest? request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        await _devAccess.SetCurrentEmailAsync(request.Email);
        var response = await BuildAccessResponseAsync();
        return response;
    }

    [HttpPost("theme")]
    public async Task<ActionResult<AccessResponse>> SetTheme([FromBody] SetThemeRequest? request)
    {
        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var ok = await _devAccess.SetThemeForCurrentEmailAsync(request.Theme);
        if (!ok)
        {
            return BadRequest("Current user is not set.");
        }

        var response = await BuildAccessResponseAsync();
        return response;
    }

    private async Task<AccessResponse> BuildAccessResponseAsync()
    {
        var email = (await _devAccess.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        var isAdmin = await _devAccess.IsAdminAsync();
        var theme = await _devAccess.GetThemeForCurrentEmailAsync();

        if (isAdmin)
        {
            var allScenarioIds = GetAllScenarioIds();
            return new AccessResponse
            {
                Email = email,
                IsAdmin = true,
                AllowedScenarioIds = allScenarioIds,
                AllowedScenarioOptions = allScenarioIds
                    .Select(id => new AllowedScenarioOptionDto
                    {
                        AssignmentId = id,
                        ScenarioId = id,
                        Label = id,
                        DueAtUtc = null,
                        IsSubmitted = false
                    })
                    .ToList(),
                Theme = theme
            };
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return new AccessResponse
            {
                Email = "",
                IsAdmin = false,
                AllowedScenarioIds = new List<string>(),
                AllowedScenarioOptions = new List<AllowedScenarioOptionDto>(),
                Theme = theme
            };
        }

        var allScenarios = GetAllScenarioIds();
        var allClasses = await _classes.GetAllAsync();

        var memberClasses = allClasses
            .Where(c => c.Students.Any(s => string.Equals(s, email, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (memberClasses.Count == 0)
        {
            return new AccessResponse
            {
                Email = email,
                IsAdmin = false,
                AllowedScenarioIds = new List<string>(),
                AllowedScenarioOptions = new List<AllowedScenarioOptionDto>(),
                Theme = theme
            };
        }

        var allowedAssignments = memberClasses
            .SelectMany(c => c.Assignments ?? new List<ClassRosterStore.ClassAssignment>())
            .Select(a => new
            {
                ScenarioId = (a.ScenarioId ?? "").Trim(),
                AssignmentName = (a.Name ?? "").Trim(),
                DueAtUtc = a.DueAtUtc,
                AssignedAtUtc = a.AssignedAtUtc
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.ScenarioId))
            .ToList();

        var filteredScenarioIds = allowedAssignments
            .Select(x => x.ScenarioId)
            .Where(id => allScenarios.Contains(id, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filteredOptions = memberClasses
            .SelectMany(c => c.Assignments ?? new List<ClassRosterStore.ClassAssignment>())
            .Select(a =>
            {
                var scenarioId = (a.ScenarioId ?? "").Trim();
                var assignmentName = (a.Name ?? "").Trim();
                var submission = a.Submissions?
                    .FirstOrDefault(s => string.Equals(s.StudentEmail, email, StringComparison.OrdinalIgnoreCase));

                return new AllowedScenarioOptionDto
                {
                    AssignmentId = a.Id,
                    ScenarioId = scenarioId,
                    Label = string.IsNullOrWhiteSpace(assignmentName) ? scenarioId : assignmentName,
                    DueAtUtc = a.DueAtUtc,
                    IsSubmitted = submission?.SubmittedAtUtc.HasValue == true
                };
            })
            .Where(x =>
                !string.IsNullOrWhiteSpace(x.AssignmentId) &&
                !string.IsNullOrWhiteSpace(x.ScenarioId) &&
                allScenarios.Contains(x.ScenarioId, StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AccessResponse
        {
            Email = email,
            IsAdmin = false,
            AllowedScenarioIds = filteredScenarioIds,
            AllowedScenarioOptions = filteredOptions,
            Theme = theme
        };
    }

    private List<string> GetAllScenarioIds()
    {
        var path = Path.Combine(_env.ContentRootPath, "Data", "scenarios");
        if (!Directory.Exists(path)) return new List<string>();

        return [.. Directory.GetFiles(path, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)];
    }
}
