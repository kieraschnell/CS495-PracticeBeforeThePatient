using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/access")]
public sealed class AccessController : ControllerBase
{
    private readonly DevAccessStore _devAccess;
    private readonly AppDbContext _db;

    public AccessController(DevAccessStore devAccess, AppDbContext db)
    {
        _devAccess = devAccess;
        _db = db;
    }

    public sealed class AccessResponse
    {
        public string Email { get; set; } = "";
        public string Role { get; set; } = DevAccessStore.StudentRole;
        public bool IsTeacher { get; set; }
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
        var role = await _devAccess.GetCurrentRoleAsync();
        var isTeacher = await _devAccess.IsTeacherAsync();
        var isAdmin = await _devAccess.IsAdminAsync();
        var theme = await _devAccess.GetThemeForCurrentEmailAsync();
        var nowUtc = DateTime.UtcNow;

        if (isTeacher)
        {
            var allScenarioIds = await GetAllScenarioIdsAsync();
            return new AccessResponse
            {
                Email = email,
                Role = role,
                IsTeacher = true,
                IsAdmin = isAdmin,
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
                Role = DevAccessStore.StudentRole,
                IsTeacher = false,
                IsAdmin = false,
                AllowedScenarioIds = new List<string>(),
                AllowedScenarioOptions = new List<AllowedScenarioOptionDto>(),
                Theme = theme
            };
        }

        var student = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (student is null)
        {
            return new AccessResponse
            {
                Email = email,
                Role = role,
                IsTeacher = false,
                IsAdmin = false,
                AllowedScenarioIds = new List<string>(),
                AllowedScenarioOptions = new List<AllowedScenarioOptionDto>(),
                Theme = theme
            };
        }

        var enrolledClassIds = await _db.ClassStudents
            .Where(cs => cs.StudentUserId == student.Id)
            .Select(cs => cs.ClassId)
            .ToListAsync();

        if (enrolledClassIds.Count == 0)
        {
            return new AccessResponse
            {
                Email = email,
                Role = role,
                IsTeacher = false,
                IsAdmin = false,
                AllowedScenarioIds = new List<string>(),
                AllowedScenarioOptions = new List<AllowedScenarioOptionDto>(),
                Theme = theme
            };
        }

        var allScenarios = await GetAllScenarioIdsAsync();
        var allScenariosSet = allScenarios.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var assignments = await _db.Assignments
            .Where(a => enrolledClassIds.Contains(a.ClassId))
            .Where(a => !a.DueAtUtc.HasValue || a.DueAtUtc.Value >= nowUtc)
            .Include(a => a.Submissions.Where(s => s.StudentUserId == student.Id))
            .OrderBy(a => a.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(a => a.Name)
            .ToListAsync();

        var filteredOptions = assignments
            .Where(a => !string.IsNullOrWhiteSpace(a.ScenarioId) && allScenariosSet.Contains(a.ScenarioId))
            .Select(a =>
            {
                var submission = a.Submissions.FirstOrDefault();
                return new AllowedScenarioOptionDto
                {
                    AssignmentId = a.Id.ToString(),
                    ScenarioId = a.ScenarioId.Trim(),
                    Label = string.IsNullOrWhiteSpace(a.Name) ? a.ScenarioId : a.Name,
                    DueAtUtc = a.DueAtUtc,
                    IsSubmitted = submission is not null
                };
            })
            .ToList();

        var filteredScenarioIds = filteredOptions
            .Select(x => x.ScenarioId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AccessResponse
        {
            Email = email,
            Role = role,
            IsTeacher = false,
            IsAdmin = false,
            AllowedScenarioIds = filteredScenarioIds,
            AllowedScenarioOptions = filteredOptions,
            Theme = theme
        };
    }

    private async Task<List<string>> GetAllScenarioIdsAsync()
    {
        return await _db.Scenarios
            .Select(s => s.Id)
            .OrderBy(x => x)
            .ToListAsync();
    }
}
