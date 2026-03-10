using System.Text.Json;
using System.Text.RegularExpressions;

namespace PracticeBeforeThePatient.Services;

public sealed class ClassRosterStore
{
    private static readonly Regex ScenarioIdPattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly string _filePath;

    public ClassRosterStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "classes.json");
    }

    public sealed class ClassRoster
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public List<string> Teachers { get; set; } = new();
        public List<string> Students { get; set; } = new();
        public List<string> AllowedScenarioIds { get; set; } = new();
        public List<ClassAssignment> Assignments { get; set; } = new();
    }

    public sealed class ClassAssignment
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public DateTimeOffset AssignedAtUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? DueAtUtc { get; set; }
        public List<AssignmentSubmission> Submissions { get; set; } = new();
    }

    public sealed class AssignmentSubmission
    {
        public string StudentEmail { get; set; } = "";
        public DateTimeOffset? SubmittedAtUtc { get; set; }
        public string SubmissionText { get; set; } = "";
        public decimal? Grade { get; set; }
        public string GradeFeedback { get; set; } = "";
        public DateTimeOffset? GradedAtUtc { get; set; }
        public string GradedByEmail { get; set; } = "";
    }

    public async Task<List<ClassRoster>> GetAllAsync()
    {
        await _gate.WaitAsync();
        try
        {
            return await ReadUnlockedAsync();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ClassRoster?> CreateClassAsync(string name, string teacherEmail)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var normalized = name.Trim();
            var normalizedTeacher = NormalizeEmail(teacherEmail);

            if (all.Any(x => string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var roster = new ClassRoster
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = normalized,
                Teachers = string.IsNullOrWhiteSpace(normalizedTeacher) ? new List<string>() : new List<string> { normalizedTeacher },
                Students = new List<string>(),
                AllowedScenarioIds = new List<string>(),
                Assignments = new List<ClassAssignment>()
            };

            all.Add(roster);
            await WriteUnlockedAsync(all);

            return roster;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteClassAsync(string classId)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            all.RemoveAll(x => x.Id == classId);
            await WriteUnlockedAsync(all);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> AddStudentAsync(string classId, string email)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return false;

            var normalized = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalized)) return false;

            if (roster.Students.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            roster.Students.Add(normalized);
            await WriteUnlockedAsync(all);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveStudentAsync(string classId, string email)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return;

            var normalized = NormalizeEmail(email);
            roster.Students.RemoveAll(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));

            await WriteUnlockedAsync(all);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> AddTeacherAsync(string classId, string email)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return false;

            var normalized = NormalizeEmail(email);
            if (string.IsNullOrWhiteSpace(normalized)) return false;

            if (roster.Teachers.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }

            roster.Teachers.Add(normalized);
            await WriteUnlockedAsync(all);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RemoveTeacherAsync(string classId, string email)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return;

            var normalized = NormalizeEmail(email);
            roster.Teachers.RemoveAll(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));
            await WriteUnlockedAsync(all);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<List<string>?> GetAllowedScenarioIdsAsync(string classId)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return null;

            roster.AllowedScenarioIds ??= new List<string>();
            return roster.AllowedScenarioIds.ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> SetAllowedScenarioIdsAsync(string classId, List<string> scenarioIds)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return false;

            var cleaned = (scenarioIds ?? new List<string>())
                .Select(x => (x ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (cleaned.Any(x => !ScenarioIdPattern.IsMatch(x)))
            {
                return false;
            }

            roster.AllowedScenarioIds = cleaned;
            await WriteUnlockedAsync(all);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<List<ClassAssignment>?> GetAssignmentsAsync(string classId)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return null;

            return roster.Assignments
                .OrderBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue)
                .ThenBy(x => x.ScenarioId, StringComparer.OrdinalIgnoreCase)
                .Select(CloneAssignment)
                .ToList();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<ClassAssignment?> CreateAssignmentAsync(string classId, string name, string scenarioId, DateTimeOffset? dueAtUtc)
    {
        await _gate.WaitAsync();
        try
        {
            var normalizedName = (name ?? "").Trim();
            var normalizedScenario = (scenarioId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalizedName) || !ScenarioIdPattern.IsMatch(normalizedScenario))
            {
                return null;
            }

            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return null;

            var assignment = new ClassAssignment
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = normalizedName,
                ScenarioId = normalizedScenario,
                AssignedAtUtc = DateTimeOffset.UtcNow,
                DueAtUtc = dueAtUtc?.ToUniversalTime(),
                Submissions = new List<AssignmentSubmission>()
            };

            roster.Assignments.Add(assignment);

            await WriteUnlockedAsync(all);
            return CloneAssignment(assignment);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> DeleteAssignmentAsync(string classId, string assignmentId)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return false;

            var removed = roster.Assignments.RemoveAll(x => x.Id == assignmentId);
            if (removed == 0) return false;

            await WriteUnlockedAsync(all);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> UpdateAssignmentDueAtAsync(string classId, string assignmentId, DateTimeOffset? dueAtUtc)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return false;

            var assignment = roster.Assignments.FirstOrDefault(x => x.Id == assignmentId);
            if (assignment is null) return false;

            assignment.DueAtUtc = dueAtUtc?.ToUniversalTime();

            await WriteUnlockedAsync(all);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> GradeAssignmentAsync(
        string classId,
        string assignmentId,
        string studentEmail,
        decimal? grade,
        string feedback,
        string gradedByEmail)
    {
        await _gate.WaitAsync();
        try
        {
            var normalizedStudent = NormalizeEmail(studentEmail);
            if (string.IsNullOrWhiteSpace(normalizedStudent)) return false;

            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return false;

            var assignment = roster.Assignments.FirstOrDefault(x => x.Id == assignmentId);
            if (assignment is null) return false;

            var submission = assignment.Submissions
                .FirstOrDefault(x => string.Equals(x.StudentEmail, normalizedStudent, StringComparison.OrdinalIgnoreCase));

            if (submission is null)
            {
                submission = new AssignmentSubmission
                {
                    StudentEmail = normalizedStudent
                };

                assignment.Submissions.Add(submission);
            }

            submission.Grade = grade;
            submission.GradeFeedback = (feedback ?? "").Trim();
            submission.GradedAtUtc = DateTimeOffset.UtcNow;
            submission.GradedByEmail = NormalizeEmail(gradedByEmail);

            await WriteUnlockedAsync(all);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<int> SubmitScenarioAsync(string studentEmail, string scenarioId, string submissionText)
    {
        await _gate.WaitAsync();
        try
        {
            var normalizedStudent = NormalizeEmail(studentEmail);
            var normalizedScenario = (scenarioId ?? "").Trim();
            if (string.IsNullOrWhiteSpace(normalizedStudent) || !ScenarioIdPattern.IsMatch(normalizedScenario))
            {
                return 0;
            }

            var all = await ReadUnlockedAsync();
            var now = DateTimeOffset.UtcNow;
            var touched = 0;

            foreach (var roster in all)
            {
                var isStudentInClass = roster.Students.Any(x => string.Equals(x, normalizedStudent, StringComparison.OrdinalIgnoreCase));
                if (!isStudentInClass) continue;

                foreach (var assignment in roster.Assignments.Where(x => string.Equals(x.ScenarioId, normalizedScenario, StringComparison.OrdinalIgnoreCase)))
                {
                    var submission = assignment.Submissions
                        .FirstOrDefault(x => string.Equals(x.StudentEmail, normalizedStudent, StringComparison.OrdinalIgnoreCase));

                    if (submission is null)
                    {
                        submission = new AssignmentSubmission
                        {
                            StudentEmail = normalizedStudent
                        };

                        assignment.Submissions.Add(submission);
                    }

                    submission.SubmittedAtUtc = now;
                    submission.SubmissionText = (submissionText ?? "").Trim();
                    touched++;
                }
            }

            if (touched > 0)
            {
                await WriteUnlockedAsync(all);
            }

            return touched;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<ClassRoster>> ReadUnlockedAsync()
    {
        if (!File.Exists(_filePath)) return new List<ClassRoster>();

        var json = await File.ReadAllTextAsync(_filePath);

        var data = JsonSerializer.Deserialize<List<ClassRoster>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        if (data is null) return new List<ClassRoster>();

        foreach (var c in data)
        {
            c.Teachers ??= new List<string>();
            c.Students ??= new List<string>();
            c.AllowedScenarioIds ??= new List<string>();
            c.Assignments ??= new List<ClassAssignment>();

            c.Teachers = c.Teachers
                .Select(NormalizeEmail)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (c.Teachers.Count == 0)
            {
                c.Teachers.Add("admin@ua.edu");
            }

            c.Students = c.Students
                .Select(NormalizeEmail)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            c.AllowedScenarioIds = c.AllowedScenarioIds
                .Select(x => (x ?? "").Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x) && ScenarioIdPattern.IsMatch(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var a in c.Assignments)
            {
                a.Id = (a.Id ?? "").Trim();
                if (string.IsNullOrWhiteSpace(a.Id))
                {
                    a.Id = Guid.NewGuid().ToString("N");
                }

                a.Name = (a.Name ?? "").Trim();
                a.ScenarioId = (a.ScenarioId ?? "").Trim();
                if (string.IsNullOrWhiteSpace(a.Name))
                {
                    a.Name = a.ScenarioId;
                }
                a.Submissions ??= new List<AssignmentSubmission>();

                a.Submissions = a.Submissions
                    .Where(x => !string.IsNullOrWhiteSpace(x.StudentEmail))
                    .Select(x =>
                    {
                        x.StudentEmail = NormalizeEmail(x.StudentEmail);
                        x.SubmissionText = x.SubmissionText ?? "";
                        x.GradeFeedback = x.GradeFeedback ?? "";
                        x.GradedByEmail = NormalizeEmail(x.GradedByEmail);
                        return x;
                    })
                    .Where(x => !string.IsNullOrWhiteSpace(x.StudentEmail))
                    .GroupBy(x => x.StudentEmail, StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.OrderByDescending(x => x.SubmittedAtUtc ?? DateTimeOffset.MinValue).First())
                    .ToList();
            }

            c.Assignments = c.Assignments
                .Where(x => !string.IsNullOrWhiteSpace(x.ScenarioId) && ScenarioIdPattern.IsMatch(x.ScenarioId))
                .ToList();
        }

        return data;
    }

    private async Task WriteUnlockedAsync(List<ClassRoster> data)
    {
        var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_filePath, json);
    }

    private static string NormalizeEmail(string email)
    {
        return (email ?? "").Trim().ToLowerInvariant();
    }

    private static ClassAssignment CloneAssignment(ClassAssignment source)
    {
        return new ClassAssignment
        {
            Id = source.Id,
            Name = source.Name,
            ScenarioId = source.ScenarioId,
            AssignedAtUtc = source.AssignedAtUtc,
            DueAtUtc = source.DueAtUtc,
            Submissions = source.Submissions
                .Select(x => new AssignmentSubmission
                {
                    StudentEmail = x.StudentEmail,
                    SubmittedAtUtc = x.SubmittedAtUtc,
                    SubmissionText = x.SubmissionText,
                    Grade = x.Grade,
                    GradeFeedback = x.GradeFeedback,
                    GradedAtUtc = x.GradedAtUtc,
                    GradedByEmail = x.GradedByEmail
                })
                .ToList()
        };
    }
}
