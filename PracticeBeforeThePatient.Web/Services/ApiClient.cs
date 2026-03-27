using PracticeBeforeThePatient.Core.Models;
using System.Net.Http.Json;

namespace PracticeBeforeThePatient.Web.Services;

public class ApiClient
{
    public HttpClient Http => _httpClient;

    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public sealed class AccessResponse
    {
        public string Email { get; set; } = "";
        public string Role { get; set; } = "student";
        public bool IsTeacher { get; set; }
        public bool IsAdmin { get; set; }
        public List<string> AllowedScenarioIds { get; set; } = new();
        public List<AllowedScenarioOption> AllowedScenarioOptions { get; set; } = new();
        public string Theme { get; set; } = "";
    }

    public sealed class AllowedScenarioOption
    {
        public string AssignmentId { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public string Label { get; set; } = "";
        public DateTimeOffset AssignedAtUtc { get; set; }
        public DateTimeOffset? DueAtUtc { get; set; }
        public bool IsSubmitted { get; set; }
    }

    public sealed class SetDevUserRequest
    {
        public string Email { get; set; } = "";
    }

    public sealed class SetThemeRequest
    {
        public string Theme { get; set; } = "light";
    }

    public async Task<AccessResponse?> GetAccessAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<AccessResponse>("api/access");
        }
        catch
        {
            return null;
        }
    }

    public async Task<AccessResponse?> SetCurrentDevUserAsync(string email)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/access/dev-user", new SetDevUserRequest
            {
                Email = email ?? ""
            });

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AccessResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<AccessResponse?> SetThemeAsync(string theme)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/access/theme", new SetThemeRequest
            {
                Theme = theme ?? "light"
            });

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            return await response.Content.ReadFromJsonAsync<AccessResponse>();
        }
        catch
        {
            return null;
        }
    }

    public async Task<Scenario?> GetScenarioAsync(string scenarioId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Scenario>($"api/scenarios/{scenarioId}");
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<string>?> GetAvailableScenariosAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<string>>("api/scenarios");
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> UpdateScenarioAsync(string scenarioId, Scenario scenario)
    {
        try
        {
            var response = await _httpClient.PutAsJsonAsync($"api/scenarios/{scenarioId}", scenario);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public sealed class SubmitScenarioRequest
    {
        public string ScenarioId { get; set; } = "";
        public string SubmissionText { get; set; } = "";
    }

    public sealed class SubmitScenarioResponse
    {
        public int UpdatedAssignments { get; set; }
    }

    public async Task<SubmitScenarioResponse?> SubmitScenarioAsync(string scenarioId, string submissionText)
    {
        try
        {
            var resp = await _httpClient.PostAsJsonAsync("api/assignments/submit-scenario", new SubmitScenarioRequest
            {
                ScenarioId = scenarioId ?? "",
                SubmissionText = submissionText ?? ""
            });

            if (!resp.IsSuccessStatusCode)
            {
                return null;
            }

            return await resp.Content.ReadFromJsonAsync<SubmitScenarioResponse>();
        }
        catch
        {
            return null;
        }
    }
}
