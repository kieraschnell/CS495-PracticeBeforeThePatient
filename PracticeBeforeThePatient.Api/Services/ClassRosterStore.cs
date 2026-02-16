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
        public List<string> Students { get; set; } = new();
        public List<string> AllowedScenarioIds { get; set; } = new();
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

    public async Task<ClassRoster?> CreateClassAsync(string name)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var normalized = name.Trim();

            if (all.Any(x => string.Equals(x.Name, normalized, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            var roster = new ClassRoster
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = normalized,
                Students = new List<string>(),
                AllowedScenarioIds = new List<string>()
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

            var normalized = (email ?? "").Trim().ToLowerInvariant();
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

            var normalized = (email ?? "").Trim().ToLowerInvariant();
            roster.Students.RemoveAll(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase));

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
            c.Students ??= new List<string>();
            c.AllowedScenarioIds ??= new List<string>();
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
}
