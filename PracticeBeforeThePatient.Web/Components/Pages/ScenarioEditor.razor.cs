using Microsoft.AspNetCore.Components;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Web.Services;

namespace PracticeBeforeThePatient.Web.Components.Pages;

public partial class ScenarioEditor : ComponentBase
{
    [Inject] private ApiClient ApiClient { get; set; } = default!;

    protected bool _isLoading = true;
    protected bool _isLoadingScenario;
    protected bool _isSaving;
    protected string? _errorMessage;
    protected string? _saveMessage;
    protected bool _saveSuccess;

    protected Scenario? _scenario;
    protected List<string> AvailableScenarioIds { get; set; } = new();
    protected string SelectedScenarioId { get; set; } = "";
    protected NodeSelectionState _nodeSelection = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadAvailableScenarios();
        _isLoading = false;
    }

    private async Task LoadAvailableScenarios()
    {
        var scenarios = await ApiClient.GetAvailableScenariosAsync();
        if (scenarios != null)
        {
            AvailableScenarioIds = scenarios
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        else
        {
            _errorMessage = "Failed to load available scenarios.";
        }
    }

    protected async Task OnScenarioChanged()
    {
        if (string.IsNullOrWhiteSpace(SelectedScenarioId))
        {
            _scenario = null;
            _nodeSelection.Clear();
            return;
        }

        _isLoadingScenario = true;
        _errorMessage = null;
        _saveMessage = null;
        StateHasChanged();

        var scenario = await ApiClient.GetScenarioAsync(SelectedScenarioId);
        if (scenario != null)
        {
            _scenario = scenario;
            _nodeSelection.Clear();
        }
        else
        {
            _errorMessage = $"Failed to load scenario '{SelectedScenarioId}'.";
        }

        _isLoadingScenario = false;
    }

    private NodeEditor? _rootNodeEditor;

    protected void ExpandAll()
    {
        _rootNodeEditor?.ExpandAllNodes();
    }

    protected void CollapseAll()
    {
        _rootNodeEditor?.CollapseAllNodes();
    }

    protected async Task SaveScenario()
    {
        if (_scenario == null || string.IsNullOrWhiteSpace(SelectedScenarioId))
        {
            return;
        }

        var validationError = ValidateScenario(_scenario);
        if (!string.IsNullOrWhiteSpace(validationError))
        {
            _saveSuccess = false;
            _saveMessage = $"Validation failed: {validationError}";
            return;
        }

        _isSaving = true;
        _saveMessage = null;
        StateHasChanged();

        var success = await ApiClient.UpdateScenarioAsync(SelectedScenarioId, _scenario);

        _saveSuccess = success;
        _saveMessage = success ? "Scenario saved successfully!" : "Failed to save scenario.";
        _isSaving = false;

        StateHasChanged();
    }

    private string? ValidateScenario(Scenario scenario)
    {
        if (string.IsNullOrWhiteSpace(scenario.Title))
        {
            return "Scenario must have a title.";
        }

        return ValidateNode(scenario.Root, "Root");
    }

    private string? ValidateNode(Node node, string path)
    {
        if (node.Type == "mcq" && (node.Choices == null || node.Choices.Count == 0))
        {
            return $"MCQ node at '{path}' must have at least one choice.";
        }

        if (node.Choices != null)
        {
            for (int i = 0; i < node.Choices.Count; i++)
            {
                var choice = node.Choices[i];
                if (string.IsNullOrWhiteSpace(choice.Label))
                {
                    return $"Choice {i + 1} at '{path}' must have a label.";
                }
                if (string.IsNullOrWhiteSpace(choice.Text))
                {
                    return $"Choice '{choice.Label}' at '{path}' must have text.";
                }
                if (choice.Next != null)
                {
                    var childError = ValidateNode(choice.Next, $"{path} → {choice.Label}");
                    if (childError != null)
                    {
                        return childError;
                    }
                }
            }
        }

        return null;
    }
}
