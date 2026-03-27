using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data;
using PracticeBeforeThePatient.Data.Entities;
using PracticeBeforeThePatient.Services;

namespace PracticeBeforeThePatient.Api.Controllers;

[ApiController]
[Route("api/classes")]
public sealed class ClassesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly DevAccessStore _access;

    public ClassesController(AppDbContext db, DevAccessStore access)
    {
        _db = db;
        _access = access;
    }

    private async Task<bool> RequireTeacher()
    {
        return await _access.IsTeacherAsync();
    }

    private async Task<bool> CanAccessClassAsync(int classId)
    {
        if (await _access.IsAdminAsync())
        {
            return true;
        }

        var currentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(currentEmail))
        {
            return false;
        }

        return await _db.ClassTeachers
            .AnyAsync(ct => ct.ClassId == classId
                && ct.Teacher.Email == currentEmail);
    }

    public sealed class ClassRosterDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public List<string> Teachers { get; set; } = new();
        public List<string> Students { get; set; } = new();
    }

    [HttpGet]
    public async Task<ActionResult<List<ClassRosterDto>>> GetAll()
    {
        if (!await RequireTeacher()) return Forbid();

        var query = _db.Classes
            .Include(c => c.Teachers).ThenInclude(ct => ct.Teacher)
            .Include(c => c.Students).ThenInclude(cs => cs.Student)
            .AsQueryable();

        if (!await _access.IsAdminAsync())
        {
            var currentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
            query = query.Where(c => c.Teachers.Any(t => t.Teacher.Email == currentEmail));
        }

        var classes = await query
            .OrderBy(c => c.Name)
            .ToListAsync();

        return classes
            .Select(c => new ClassRosterDto
            {
                Id = c.Id,
                Name = c.Name,
                Teachers = c.Teachers.Select(t => t.Teacher.Email).ToList(),
                Students = c.Students.Select(s => s.Student.Email).ToList()
            })
            .ToList();
    }

    public sealed class CreateClassRequest
    {
        public string Name { get; set; } = "";
    }

    [HttpPost]
    public async Task<ActionResult<ClassRosterDto>> Create([FromBody] CreateClassRequest req)
    {
        if (!await RequireTeacher()) return Forbid();

        var normalized = (req.Name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return BadRequest("Class name cannot be empty.");
        }

        if (await _db.Classes.AnyAsync(c => c.Name == normalized))
        {
            return Conflict("A class with that name already exists.");
        }

        var currentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.Email == currentEmail);
        if (currentUser is null) return BadRequest("Current user not found.");

        var classEntity = new ClassEntity
        {
            Name = normalized,
            CreatedByUserId = currentUser.Id,
            CreatedAtUtc = DateTime.UtcNow
        };
        _db.Classes.Add(classEntity);
        await _db.SaveChangesAsync();

        _db.ClassTeachers.Add(new ClassTeacherEntity
        {
            ClassId = classEntity.Id,
            TeacherUserId = currentUser.Id,
            AddedByUserId = currentUser.Id,
            AddedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return new ClassRosterDto
        {
            Id = classEntity.Id,
            Name = classEntity.Name,
            Teachers = [currentEmail],
            Students = []
        };
    }

    public sealed class StudentRequest
    {
        public string Email { get; set; } = "";
    }

    [HttpPost("{classId:int}/students")]
    public async Task<IActionResult> AddStudent(int classId, [FromBody] StudentRequest req)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) return BadRequest();

        var student = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (student is null)
        {
            student = new UserEntity
            {
                Email = email,
                Name = email,
                Role = DevAccessStore.StudentRole,
                SsoSubject = $"dev-{Guid.NewGuid():N}"
            };
            _db.Users.Add(student);
            await _db.SaveChangesAsync();
        }

        if (await _db.ClassStudents.AnyAsync(cs => cs.ClassId == classId && cs.StudentUserId == student.Id))
        {
            return BadRequest();
        }

        var currentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        var currentUser = await _db.Users.FirstAsync(u => u.Email == currentEmail);

        _db.ClassStudents.Add(new ClassStudentEntity
        {
            ClassId = classId,
            StudentUserId = student.Id,
            AddedByUserId = currentUser.Id,
            AddedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{classId:int}/students")]
    public async Task<IActionResult> RemoveStudent(int classId, [FromBody] StudentRequest req)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var enrollment = await _db.ClassStudents
            .FirstOrDefaultAsync(cs => cs.ClassId == classId && cs.Student.Email == email);

        if (enrollment is not null)
        {
            _db.ClassStudents.Remove(enrollment);
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    [HttpDelete("{classId:int}")]
    public async Task<IActionResult> Delete(int classId)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        var classEntity = await _db.Classes
            .Include(c => c.Teachers)
            .Include(c => c.Students)
            .Include(c => c.Assignments).ThenInclude(a => a.Submissions)
            .FirstOrDefaultAsync(c => c.Id == classId);

        if (classEntity is null) return NotFound();

        _db.Submissions.RemoveRange(classEntity.Assignments.SelectMany(a => a.Submissions));
        _db.Assignments.RemoveRange(classEntity.Assignments);
        _db.ClassTeachers.RemoveRange(classEntity.Teachers);
        _db.ClassStudents.RemoveRange(classEntity.Students);
        _db.Classes.Remove(classEntity);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    public sealed class AssignmentSubmissionDto
    {
        public string StudentEmail { get; set; } = "";
        public DateTimeOffset? SubmittedAtUtc { get; set; }
        public string SubmissionText { get; set; } = "";
        public decimal? Grade { get; set; }
        public string GradeFeedback { get; set; } = "";
        public DateTimeOffset? GradedAtUtc { get; set; }
        public string GradedByEmail { get; set; } = "";
    }

    public sealed class AssignmentDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public DateTimeOffset AssignedAtUtc { get; set; }
        public DateTimeOffset? DueAtUtc { get; set; }
        public List<AssignmentSubmissionDto> Submissions { get; set; } = new();
    }

    [HttpGet("{classId:int}/assignments")]
    public async Task<ActionResult<List<AssignmentDto>>> GetAssignments(int classId)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        if (!await _db.Classes.AnyAsync(c => c.Id == classId)) return NotFound();

        var assignments = await _db.Assignments
            .Where(a => a.ClassId == classId)
            .Include(a => a.Submissions).ThenInclude(s => s.Student)
            .Include(a => a.Submissions).ThenInclude(s => s.GradedBy)
            .OrderBy(a => a.AssignedAtUtc)
            .ThenBy(a => a.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(a => a.ScenarioId)
            .ToListAsync();

        return assignments
            .Select(a => new AssignmentDto
            {
                Id = a.Id,
                Name = a.Name,
                ScenarioId = a.ScenarioId,
                AssignedAtUtc = a.AssignedAtUtc,
                DueAtUtc = a.DueAtUtc,
                Submissions = a.Submissions
                    .OrderBy(s => s.Student.Email, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new AssignmentSubmissionDto
                    {
                        StudentEmail = s.Student.Email,
                        SubmittedAtUtc = s.SubmittedAtUtc,
                        SubmissionText = s.SubmissionText,
                        Grade = s.Grade,
                        GradeFeedback = s.GradeFeedback ?? "",
                        GradedAtUtc = s.GradedAtUtc,
                        GradedByEmail = s.GradedBy?.Email ?? ""
                    })
                    .ToList()
            })
            .ToList();
    }

    public sealed class CreateAssignmentRequest
    {
        public string Name { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public DateTimeOffset? AssignedAtUtc { get; set; }
        public DateTimeOffset? DueAtUtc { get; set; }
    }

    [HttpPost("{classId:int}/assignments")]
    public async Task<ActionResult<AssignmentDto>> CreateAssignment(int classId, [FromBody] CreateAssignmentRequest req)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        var assignmentName = (req.Name ?? "").Trim();
        var scenarioId = (req.ScenarioId ?? "").Trim();
        var assignedAtUtc = req.AssignedAtUtc?.UtcDateTime ?? DateTime.UtcNow;
        var dueAtUtc = req.DueAtUtc?.UtcDateTime;

        if (string.IsNullOrWhiteSpace(assignmentName))
        {
            return BadRequest("Assignment name is required.");
        }

        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            return BadRequest("Scenario id is required.");
        }

        if (!await _db.Classes.AnyAsync(c => c.Id == classId))
        {
            return BadRequest("Invalid class id.");
        }

        if (!await _db.Scenarios.AnyAsync(s => s.Id == scenarioId))
        {
            return BadRequest("Invalid scenario id.");
        }

        if (dueAtUtc.HasValue && dueAtUtc.Value < assignedAtUtc)
        {
            return BadRequest("Due date must be after the assign date.");
        }

        var currentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        var currentUser = await _db.Users.FirstAsync(u => u.Email == currentEmail);

        var assignment = new AssignmentEntity
        {
            ClassId = classId,
            ScenarioId = scenarioId,
            Name = assignmentName,
            AssignedAtUtc = assignedAtUtc,
            DueAtUtc = dueAtUtc,
            AssignedByUserId = currentUser.Id
        };

        _db.Assignments.Add(assignment);
        await _db.SaveChangesAsync();

        return new AssignmentDto
        {
            Id = assignment.Id,
            Name = assignment.Name,
            ScenarioId = assignment.ScenarioId,
            AssignedAtUtc = assignment.AssignedAtUtc,
            DueAtUtc = assignment.DueAtUtc,
            Submissions = []
        };
    }

    [HttpDelete("{classId:int}/assignments/{assignmentId:int}")]
    public async Task<IActionResult> DeleteAssignment(int classId, int assignmentId)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        var assignment = await _db.Assignments
            .Include(a => a.Submissions)
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.ClassId == classId);

        if (assignment is null) return NotFound();

        _db.Submissions.RemoveRange(assignment.Submissions);
        _db.Assignments.Remove(assignment);
        await _db.SaveChangesAsync();

        return NoContent();
    }

    public sealed class UpdateAssignmentDueRequest
    {
        public DateTimeOffset? DueAtUtc { get; set; }
    }

    [HttpPut("{classId:int}/assignments/{assignmentId:int}/due")]
    public async Task<IActionResult> UpdateAssignmentDue(int classId, int assignmentId, [FromBody] UpdateAssignmentDueRequest req)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        var assignment = await _db.Assignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.ClassId == classId);

        if (assignment is null) return NotFound();

        var dueAtUtc = req.DueAtUtc?.UtcDateTime;
        if (dueAtUtc.HasValue && dueAtUtc.Value < assignment.AssignedAtUtc)
        {
            return BadRequest("Due date must be after the assign date.");
        }

        assignment.DueAtUtc = dueAtUtc;
        await _db.SaveChangesAsync();

        return NoContent();
    }

    public sealed class GradeAssignmentRequest
    {
        public string StudentEmail { get; set; } = "";
        public decimal? Grade { get; set; }
        public string Feedback { get; set; } = "";
    }

    [HttpPut("{classId:int}/assignments/{assignmentId:int}/grades")]
    public async Task<IActionResult> GradeAssignment(int classId, int assignmentId, [FromBody] GradeAssignmentRequest req)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        if (string.IsNullOrWhiteSpace(req.StudentEmail))
        {
            return BadRequest("Student email is required.");
        }

        if (req.Grade is < 0 or > 100)
        {
            return BadRequest("Grade must be between 0 and 100.");
        }

        var studentEmail = req.StudentEmail.Trim().ToLowerInvariant();
        var student = await _db.Users.FirstOrDefaultAsync(u => u.Email == studentEmail);
        if (student is null) return BadRequest("Student not found.");

        var assignment = await _db.Assignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId && a.ClassId == classId);
        if (assignment is null) return BadRequest("Invalid class or assignment.");

        var graderEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        var grader = await _db.Users.FirstAsync(u => u.Email == graderEmail);

        var submission = await _db.Submissions
            .FirstOrDefaultAsync(s => s.AssignmentId == assignmentId && s.StudentUserId == student.Id);

        if (submission is null)
        {
            submission = new SubmissionEntity
            {
                AssignmentId = assignmentId,
                StudentUserId = student.Id,
                SubmittedAtUtc = DateTime.UtcNow,
                SubmissionText = ""
            };
            _db.Submissions.Add(submission);
        }

        submission.Grade = req.Grade;
        submission.GradeFeedback = (req.Feedback ?? "").Trim();
        submission.GradedAtUtc = DateTime.UtcNow;
        submission.GradedByUserId = grader.Id;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("{classId:int}/teachers")]
    public async Task<IActionResult> AddTeacher(int classId, [FromBody] StudentRequest req)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email)) return BadRequest();

        var teacher = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (teacher is null)
        {
            teacher = new UserEntity
            {
                Email = email,
                Name = email,
                Role = DevAccessStore.TeacherRole,
                SsoSubject = $"dev-{Guid.NewGuid():N}"
            };
            _db.Users.Add(teacher);
            await _db.SaveChangesAsync();
        }

        if (await _db.ClassTeachers.AnyAsync(ct => ct.ClassId == classId && ct.TeacherUserId == teacher.Id))
        {
            return BadRequest();
        }

        var currentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        var currentUser = await _db.Users.FirstAsync(u => u.Email == currentEmail);

        _db.ClassTeachers.Add(new ClassTeacherEntity
        {
            ClassId = classId,
            TeacherUserId = teacher.Id,
            AddedByUserId = currentUser.Id,
            AddedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{classId:int}/teachers")]
    public async Task<IActionResult> RemoveTeacher(int classId, [FromBody] StudentRequest req)
    {
        if (!await CanAccessClassAsync(classId)) return Forbid();

        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var currentEmail = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.Equals(email, currentEmail, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("You cannot remove yourself from the class.");
        }

        var ct = await _db.ClassTeachers
            .FirstOrDefaultAsync(ct => ct.ClassId == classId && ct.Teacher.Email == email);

        if (ct is not null)
        {
            _db.ClassTeachers.Remove(ct);
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    public sealed class StudentGradeDto
    {
        public int ClassId { get; set; }
        public string ClassName { get; set; } = "";
        public int AssignmentId { get; set; }
        public string AssignmentName { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public DateTimeOffset AssignedAtUtc { get; set; }
        public DateTimeOffset? DueAtUtc { get; set; }
        public DateTimeOffset? SubmittedAtUtc { get; set; }
        public string SubmissionText { get; set; } = "";
        public decimal? Grade { get; set; }
        public string GradeFeedback { get; set; } = "";
        public DateTimeOffset? GradedAtUtc { get; set; }
        public string GradedByEmail { get; set; } = "";
    }

    [HttpGet("me/grades")]
    public async Task<ActionResult<List<StudentGradeDto>>> GetMyGrades()
    {
        var email = (await _access.GetCurrentEmailAsync()).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
        {
            return Ok(new List<StudentGradeDto>());
        }

        var student = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (student is null)
        {
            return Ok(new List<StudentGradeDto>());
        }

        var enrolledClassIds = await _db.ClassStudents
            .Where(cs => cs.StudentUserId == student.Id)
            .Select(cs => cs.ClassId)
            .ToListAsync();

        var assignments = await _db.Assignments
            .Where(a => enrolledClassIds.Contains(a.ClassId))
            .Where(a => a.AssignedAtUtc <= DateTime.UtcNow)
            .Include(a => a.Class)
            .Include(a => a.Submissions.Where(s => s.StudentUserId == student.Id))
                .ThenInclude(s => s.GradedBy)
            .OrderBy(a => a.AssignedAtUtc)
            .ThenBy(a => a.DueAtUtc ?? DateTime.MaxValue)
            .ThenBy(a => a.Name)
            .ToListAsync();

        return assignments
            .Select(a =>
            {
                var submission = a.Submissions.FirstOrDefault();
                return new StudentGradeDto
                {
                    ClassId = a.ClassId,
                    ClassName = a.Class.Name,
                    AssignmentId = a.Id,
                    AssignmentName = a.Name,
                    ScenarioId = a.ScenarioId,
                    AssignedAtUtc = a.AssignedAtUtc,
                    DueAtUtc = a.DueAtUtc,
                    SubmittedAtUtc = submission?.SubmittedAtUtc,
                    SubmissionText = submission?.SubmissionText ?? "",
                    Grade = submission?.Grade,
                    GradeFeedback = submission?.GradeFeedback ?? "",
                    GradedAtUtc = submission?.GradedAtUtc,
                    GradedByEmail = submission?.GradedBy?.Email ?? ""
                };
            })
            .ToList();
    }
}
