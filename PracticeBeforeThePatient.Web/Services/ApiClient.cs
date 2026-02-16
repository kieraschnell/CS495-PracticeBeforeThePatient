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
        public bool IsAdmin { get; set; }
        public List<string> AllowedScenarioIds { get; set; } = new();
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
}