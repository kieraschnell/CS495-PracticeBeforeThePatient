using Microsoft.AspNetCore.Components;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Web.Services;

namespace PracticeBeforeThePatient.Web.Components.Pages;

public partial class Simulation : ComponentBase
{
    [Inject] private ApiClient ApiClient { get; set; } = default!;

    private Scenario? _scenario;
    private bool _isLoading = true;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _scenario = await ApiClient.GetScenarioAsync("testScenario");

            if (_scenario == null)
            {
                _errorMessage = "Scenario not found or returned null.";
            }
        }
        catch (HttpRequestException httpEx)
        {
            _errorMessage = $"HTTP Error: {httpEx.Message}\n\nMake sure the API is running on the correct port.";
        }
        catch (Exception ex)
        {
            _errorMessage = $"Error loading scenario: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }
}