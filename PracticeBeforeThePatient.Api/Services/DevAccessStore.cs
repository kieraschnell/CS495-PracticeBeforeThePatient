using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PracticeBeforeThePatient.Data;

namespace PracticeBeforeThePatient.Services;

public sealed class DevAccessStore
{
    public const string SessionHeaderName = "X-PBP-Access-Session";
    public const string LightTheme = "light";
    public const string DarkTheme = "dark";
    public const string StudentRole = "student";
    public const string TeacherRole = "teacher";
    public const string AdminRole = "admin";
    private const string DefaultEmail = "admin@ua.edu";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TemporaryAccessOptions _options;
    private readonly Lock _lock = new();

    private string _currentEmail = DefaultEmail;
    private readonly Dictionary<string, string> _themeByEmail = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SessionState> _sessionById = new(StringComparer.Ordinal);

    public DevAccessStore(
        IServiceScopeFactory scopeFactory,
        IHttpContextAccessor httpContextAccessor,
        IOptions<TemporaryAccessOptions> options)
    {
        _scopeFactory = scopeFactory;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public Task<string> GetCurrentEmailAsync()
    {
        lock (_lock)
        {
            if (TryGetSessionStateLocked(out var session))
            {
                return Task.FromResult(session.Email);
            }

            return Task.FromResult(_currentEmail);
        }
    }

    public async Task<string> GetCurrentRoleAsync()
    {
        string email;
        string? sessionRole = null;
        lock (_lock)
        {
            if (TryGetSessionStateLocked(out var session))
            {
                email = session.Email;
                sessionRole = session.Role;
            }
            else
            {
                email = _currentEmail;
            }
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            return string.IsNullOrWhiteSpace(sessionRole) ? StudentRole : NormalizeRole(sessionRole);
        }

        if (!string.IsNullOrWhiteSpace(sessionRole)
            && string.Equals(sessionRole, AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            return AdminRole;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        return NormalizeRole(user?.Role);
    }

    public async Task<bool> IsTeacherAsync()
    {
        var role = await GetCurrentRoleAsync();
        return string.Equals(role, TeacherRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<bool> IsAdminAsync()
    {
        var role = await GetCurrentRoleAsync();
        return string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase);
    }

    public Task SetCurrentEmailAsync(string email)
    {
        lock (_lock)
        {
            var normalizedEmail = (email ?? "").Trim().ToLowerInvariant();
            if (TryGetSessionId(out var sessionId))
            {
                _sessionById[sessionId] = new SessionState
                {
                    Email = normalizedEmail,
                    Role = StudentRole,
                    Theme = GetThemeForEmail(normalizedEmail)
                };
                return Task.CompletedTask;
            }

            _currentEmail = normalizedEmail;
        }
        return Task.CompletedTask;
    }

    public Task<string> GetThemeForCurrentEmailAsync()
    {
        lock (_lock)
        {
            if (TryGetSessionStateLocked(out var session))
            {
                return Task.FromResult(session.Theme);
            }

            return Task.FromResult(GetThemeForEmail(_currentEmail));
        }
    }

    public Task<bool> SetThemeForCurrentEmailAsync(string theme)
    {
        lock (_lock)
        {
            if (TryGetSessionId(out var sessionId))
            {
                var existing = GetOrCreateGuestSessionStateLocked(sessionId);
                existing.Theme = NormalizeTheme(theme);
                _sessionById[sessionId] = existing;
                return Task.FromResult(true);
            }

            var email = _currentEmail.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(email))
            {
                return Task.FromResult(false);
            }

            _themeByEmail[email] = NormalizeTheme(theme);
            return Task.FromResult(true);
        }
    }

    public Task<bool> TryLoginAdminAsync(string username, string password)
    {
        lock (_lock)
        {
            if (!TryGetSessionId(out var sessionId))
            {
                return Task.FromResult(false);
            }

            var normalizedUsername = (username ?? "").Trim();
            var normalizedPassword = password ?? "";

            if (!string.Equals(normalizedUsername, _options.AdminUsername, StringComparison.Ordinal)
                || !string.Equals(normalizedPassword, _options.AdminPassword, StringComparison.Ordinal))
            {
                return Task.FromResult(false);
            }

            var existing = GetOrCreateGuestSessionStateLocked(sessionId);
            existing.Email = DefaultEmail;
            existing.Role = AdminRole;
            _sessionById[sessionId] = existing;
            return Task.FromResult(true);
        }
    }

    public Task LogoutAsync()
    {
        lock (_lock)
        {
            if (TryGetSessionId(out var sessionId))
            {
                var existing = GetOrCreateGuestSessionStateLocked(sessionId);
                existing.Email = "";
                existing.Role = StudentRole;
                _sessionById[sessionId] = existing;
            }
        }

        return Task.CompletedTask;
    }

    public Task<bool> IsGuestAsync()
    {
        lock (_lock)
        {
            if (TryGetSessionStateLocked(out var session))
            {
                return Task.FromResult(string.IsNullOrWhiteSpace(session.Email));
            }

            return Task.FromResult(string.IsNullOrWhiteSpace(_currentEmail));
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

    private bool TryGetSessionStateLocked(out SessionState sessionState)
    {
        sessionState = default!;
        if (!TryGetSessionId(out var sessionId))
        {
            return false;
        }

        sessionState = GetOrCreateGuestSessionStateLocked(sessionId);
        return true;
    }

    private SessionState GetOrCreateGuestSessionStateLocked(string sessionId)
    {
        if (_sessionById.TryGetValue(sessionId, out var existing))
        {
            return existing;
        }

        var session = new SessionState
        {
            Email = "",
            Role = StudentRole,
            Theme = LightTheme
        };
        _sessionById[sessionId] = session;
        return session;
    }

    private bool TryGetSessionId(out string sessionId)
    {
        sessionId = "";

        var headerValue = _httpContextAccessor.HttpContext?.Request.Headers[SessionHeaderName].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        sessionId = headerValue.Trim();
        return !string.IsNullOrWhiteSpace(sessionId);
    }

    public static string NormalizeRole(string? role)
    {
        if (string.Equals(role, AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            return AdminRole;
        }

        if (string.Equals(role, TeacherRole, StringComparison.OrdinalIgnoreCase)
            || string.Equals(role, "instructor", StringComparison.OrdinalIgnoreCase))
        {
            return TeacherRole;
        }

        return StudentRole;
    }

    private sealed class SessionState
    {
        public string Email { get; set; } = "";
        public string Role { get; set; } = StudentRole;
        public string Theme { get; set; } = LightTheme;
    }
}

public sealed class TemporaryAccessOptions
{
    public string AdminUsername { get; set; } = "admin";
    public string AdminPassword { get; set; } = "change-me";
}
