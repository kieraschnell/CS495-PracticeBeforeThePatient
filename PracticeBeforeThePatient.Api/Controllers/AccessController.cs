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
    }

    [HttpGet]
    public async Task<ActionResult<AccessResponse>> Get()
    {
        var email = (await _devAccess.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        var isAdmin = await _devAccess.IsAdminAsync();

        if (isAdmin)
        {
            return new AccessResponse
            {
                Email = email,
                IsAdmin = true,
                AllowedScenarioIds = GetAllScenarioIds()
            };
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return new AccessResponse
            {
                Email = "",
                IsAdmin = false,
                AllowedScenarioIds = new List<string>()
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
                AllowedScenarioIds = new List<string>()
            };
        }

        var anyAll = memberClasses.Any(c => c.AllowedScenarioIds is null || c.AllowedScenarioIds.Count == 0);

        if (anyAll)
        {
            return new AccessResponse
            {
                Email = email,
                IsAdmin = false,
                AllowedScenarioIds = allScenarios
            };
        }

        var allowed = memberClasses
            .SelectMany(c => c.AllowedScenarioIds ?? new List<string>())
            .Select(x => (x ?? "").Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filtered = allowed
            .Where(id => allScenarios.Contains(id, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new AccessResponse
        {
            Email = email,
            IsAdmin = false,
            AllowedScenarioIds = filtered
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
