using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Data.Entities;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
public sealed class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DevAccessStore _access;

    public AdminUsersController(AppDbContext db, DevAccessStore access)
    {
        _db = db;
        _access = access;
    }

    public sealed class AdminUserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = DevAccessStore.StudentRole;
    }

    public sealed class UpsertAdminUserRequest
    {
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = DevAccessStore.TeacherRole;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminUserDto>>> GetAll()
    {
        if (!await _access.IsAdminAsync())
        {
            return Forbid();
        }

        return await _db.Users
            .OrderBy(x => x.Email)
            .Select(x => new AdminUserDto
            {
                Id = x.Id,
                Email = x.Email,
                Name = x.Name,
                Role = DevAccessStore.NormalizeRole(x.Role)
            })
            .ToListAsync();
    }

    [HttpPost]
    public async Task<ActionResult<AdminUserDto>> Upsert([FromBody] UpsertAdminUserRequest? request)
    {
        if (!await _access.IsAdminAsync())
        {
            return Forbid();
        }

        if (request is null)
        {
            return BadRequest("Request body is required.");
        }

        var email = (request.Email ?? "").Trim().ToLowerInvariant();
        var role = DevAccessStore.NormalizeRole(request.Role);
        var name = (request.Name ?? "").Trim();

        if (!EmailValidator.LooksLikeEmail(email))
        {
            return BadRequest("A valid email address is required.");
        }

        if (!string.Equals(role, DevAccessStore.TeacherRole, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(role, DevAccessStore.AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("This page only manages teacher and admin access.");
        }

        var existing = await _db.Users.FirstOrDefaultAsync(x => x.Email == email);
        if (existing is null)
        {
            existing = new UserEntity
            {
                Email = email,
                Name = name,
                Role = role,
                SsoSubject = $"dev-{Guid.NewGuid():N}"
            };

            _db.Users.Add(existing);
        }
        else
        {
            existing.Role = role;
            if (!string.IsNullOrWhiteSpace(name))
            {
                existing.Name = name;
            }
        }

        await _db.SaveChangesAsync();

        return new AdminUserDto
        {
            Id = existing.Id,
            Email = existing.Email,
            Name = existing.Name,
            Role = DevAccessStore.NormalizeRole(existing.Role)
        };
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> RemoveElevatedAccess(int id)
    {
        if (!await _access.IsAdminAsync())
        {
            return Forbid();
        }

        var existing = await _db.Users.FirstOrDefaultAsync(x => x.Id == id);
        if (existing is null)
        {
            return NotFound();
        }

        var currentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.Equals(existing.Email, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("You cannot remove your own admin access.");
        }

        existing.Role = DevAccessStore.StudentRole;
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
