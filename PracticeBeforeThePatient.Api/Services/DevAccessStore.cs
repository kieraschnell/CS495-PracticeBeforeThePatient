using Microsoft.EntityFrameworkCore;
using PracticeBeforeThePatient.Data;

namespace PracticeBeforeThePatient.Services;

public sealed class DevAccessStore
{
    public const string LightTheme = "light";
    public const string DarkTheme = "dark";
    private const string DefaultEmail = "admin@ua.edu";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Lock _lock = new();

    private string _currentEmail = DefaultEmail;
    private readonly Dictionary<string, string> _themeByEmail = new(StringComparer.OrdinalIgnoreCase);

    public DevAccessStore(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public Task<string> GetCurrentEmailAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_currentEmail);
        }
    }

    public async Task<bool> IsAdminAsync()
    {
        string email;
        lock (_lock)
        {
            email = _currentEmail;
        }

        if (string.IsNullOrWhiteSpace(email)) return false;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        return user is not null && string.Equals(user.Role, "instructor", StringComparison.OrdinalIgnoreCase);
    }

    public Task SetCurrentEmailAsync(string email)
    {
        lock (_lock)
        {
            _currentEmail = (email ?? "").Trim().ToLowerInvariant();
        }
        return Task.CompletedTask;
    }

    public Task<string> GetThemeForCurrentEmailAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(GetThemeForEmail(_currentEmail));
        }
    }

    public Task<bool> SetThemeForCurrentEmailAsync(string theme)
    {
        lock (_lock)
        {
            var email = _currentEmail.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                return Task.FromResult(false);
            }

            _themeByEmail[email] = NormalizeTheme(theme);
            return Task.FromResult(true);
        }
    }

    private string GetThemeForEmail(string email)
    {
        var normalized = (email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized)) return LightTheme;
        return _themeByEmail.TryGetValue(normalized, out var theme) ? NormalizeTheme(theme) : LightTheme;
    }

    private static string NormalizeTheme(string theme)
    {
        return string.Equals(theme, DarkTheme, StringComparison.OrdinalIgnoreCase) ? DarkTheme : LightTheme;
    }
}
