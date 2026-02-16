using System.Text.Json;
using PracticeBeforeThePatient.Core.Models;

namespace PracticeBeforeThePatient.Services;

public sealed class ClassRosterStore
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public ClassRosterStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "class_rosters.json");
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
                Students = new List<string>()
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

    public async Task<bool> DeleteClassAsync(string classId)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var removed = all.RemoveAll(x => x.Id == classId) > 0;
            if (removed) await WriteUnlockedAsync(all);
            return removed;
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

            var normalized = email.Trim().ToLowerInvariant();
            if (roster.Students.Any(x => string.Equals(x, normalized, StringComparison.OrdinalIgnoreCase)))
                return false;

            roster.Students.Add(normalized);
            await WriteUnlockedAsync(all);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RemoveStudentAsync(string classId, string email)
    {
        await _gate.WaitAsync();
        try
        {
            var all = await ReadUnlockedAsync();
            var roster = all.FirstOrDefault(x => x.Id == classId);
            if (roster is null) return false;

            var removed = roster.Students.RemoveAll(x => string.Equals(x, email, StringComparison.OrdinalIgnoreCase)) > 0;
            if (removed) await WriteUnlockedAsync(all);
            return removed;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<List<ClassRoster>> ReadUnlockedAsync()
    {
        if (!File.Exists(_filePath))
            return new List<ClassRoster>();

        var json = await File.ReadAllTextAsync(_filePath);
        if (string.IsNullOrWhiteSpace(json))
            return new List<ClassRoster>();

        var data = JsonSerializer.Deserialize<List<ClassRoster>>(json, _jsonOptions);
        return data ?? new List<ClassRoster>();
    }

    private async Task WriteUnlockedAsync(List<ClassRoster> all)
    {
        var json = JsonSerializer.Serialize(all, _jsonOptions);
        await File.WriteAllTextAsync(_filePath, json);
    }
}
