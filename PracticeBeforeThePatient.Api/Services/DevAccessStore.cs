using System.Text.Json;

namespace PracticeBeforeThePatient.Services;

public sealed class DevAccessStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public DevAccessStore(IWebHostEnvironment env)
    {
        var dataDir = Path.Combine(env.ContentRootPath, "Data");
        Directory.CreateDirectory(dataDir);
        _path = Path.Combine(dataDir, "dev-access.json");
    }

    public sealed class DevAccessConfig
    {
        public string CurrentEmail { get; set; } = "";
        public List<string> AdminEmails { get; set; } = new();
    }

    public async Task<DevAccessConfig> GetAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (!File.Exists(_path))
            {
                var seed = new DevAccessConfig
                {
                    CurrentEmail = "student@ua.edu",
                    AdminEmails = new List<string> { "admin@ua.edu" }
                };

                await WriteUnlockedAsync(seed);
                return seed;
            }

            var json = await File.ReadAllTextAsync(_path);

            var cfg = JsonSerializer.Deserialize<DevAccessConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new DevAccessConfig();

            cfg.AdminEmails ??= new List<string>();
            cfg.CurrentEmail ??= "";

            cfg.CurrentEmail = cfg.CurrentEmail.Trim().ToLowerInvariant();
            cfg.AdminEmails = cfg.AdminEmails
                .Select(x => (x ?? "").Trim().ToLowerInvariant())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return cfg;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetCurrentEmailAsync()
    {
        var cfg = await GetAsync();
        return cfg.CurrentEmail;
    }

    public async Task<bool> IsAdminAsync()
    {
        var cfg = await GetAsync();
        if (string.IsNullOrWhiteSpace(cfg.CurrentEmail)) return false;
        return cfg.AdminEmails.Contains(cfg.CurrentEmail, StringComparer.OrdinalIgnoreCase);
    }

    private async Task WriteUnlockedAsync(DevAccessConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_path, json);
    }
}
