using System.Text.Json;

namespace PracticeBeforeThePatient.Services;

public sealed class DevAccessStore
{
    public const string LightTheme = "light";
    public const string DarkTheme = "dark";

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
        public Dictionary<string, string> ThemeByEmail { get; set; } = new();
    }

    public async Task<DevAccessConfig> GetAsync()
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

    public async Task SetCurrentEmailAsync(string email)
    {
        await _gate.WaitAsync();
        try
        {
            var cfg = await ReadUnlockedAsync();
            cfg.CurrentEmail = (email ?? "").Trim().ToLowerInvariant();
            await WriteUnlockedAsync(cfg);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string> GetThemeForCurrentEmailAsync()
    {
        var cfg = await GetAsync();
        return GetThemeForEmail(cfg, cfg.CurrentEmail);
    }

    public async Task<bool> SetThemeForCurrentEmailAsync(string theme)
    {
        await _gate.WaitAsync();
        try
        {
            var cfg = await ReadUnlockedAsync();
            var email = (cfg.CurrentEmail ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            cfg.ThemeByEmail[email] = NormalizeTheme(theme);
            await WriteUnlockedAsync(cfg);
            return true;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DevAccessConfig> ReadUnlockedAsync()
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
        cfg.ThemeByEmail ??= new Dictionary<string, string>();

        cfg.CurrentEmail = cfg.CurrentEmail.Trim().ToLowerInvariant();
        cfg.AdminEmails = cfg.AdminEmails
            .Select(x => (x ?? "").Trim().ToLowerInvariant())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        cfg.ThemeByEmail = cfg.ThemeByEmail
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
            .ToDictionary(
                kvp => (kvp.Key ?? "").Trim().ToLowerInvariant(),
                kvp => NormalizeTheme(kvp.Value),
                StringComparer.OrdinalIgnoreCase);

        return cfg;
    }

    private async Task WriteUnlockedAsync(DevAccessConfig cfg)
    {
        var json = JsonSerializer.Serialize(cfg, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_path, json);
    }

    private static string GetThemeForEmail(DevAccessConfig cfg, string email)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return LightTheme;
        }

        if (cfg.ThemeByEmail.TryGetValue(normalized, out var theme))
        {
            return NormalizeTheme(theme);
        }

        return LightTheme;
    }

    private static string NormalizeTheme(string theme)
    {
        return string.Equals(theme, DarkTheme, StringComparison.OrdinalIgnoreCase) ? DarkTheme : LightTheme;
    }
}
