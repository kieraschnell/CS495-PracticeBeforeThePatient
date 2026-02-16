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

    public async Task<Scenario?> GetScenarioAsync(string scenarioId)
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<Scenario>($"api/scenarios/{scenarioId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching scenario: {ex.Message}");
            return null;
        }
    }

    public async Task<List<string>?> GetAvailableScenariosAsync()
    {
        try
        {
            return await _httpClient.GetFromJsonAsync<List<string>>("api/scenarios");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching scenarios: {ex.Message}");
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
        catch (Exception ex)
        {
            Console.WriteLine($"Error updating scenario: {ex.Message}");
            return false;
        }
    }
}