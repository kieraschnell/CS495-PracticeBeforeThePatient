using Microsoft.AspNetCore.Components;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Web.Services;
using System.Text.RegularExpressions;

namespace PracticeBeforeThePatient.Web.Components.Pages;

public partial class ScenarioEditor : ComponentBase
{
    private static readonly Regex ScenarioIdPattern = new("^[a-zA-Z0-9_-]+$", RegexOptions.Compiled);

    [Inject] private ApiClient ApiClient { get; set; } = default!;

    protected bool _isLoadingAccess = true;
    protected bool _isTeacher;
    protected bool _isLoading = true;
    protected bool _isLoadingScenario;
    protected bool _isSaving;
    protected bool _isGenerating;
    protected string? _errorMessage;
    protected string? _saveMessage;
    protected bool _saveSuccess;
    private int _saveMessageVersion;
    protected string? _generationMessage;
    protected string _generationMessageKind = "info";

    protected Scenario? _scenario;
    protected List<string> AvailableScenarioIds { get; set; } = new();
    protected string SelectedScenarioId { get; set; } = "";
    protected string GenerateTopic { get; set; } = "";
    protected string GenerateScenarioId { get; set; } = "";
    protected int GenerateMaxDepth { get; set; } = 2;
    protected NodeSelectionState _nodeSelection = new();

    protected override async Task OnInitializedAsync()
    {
        var access = await ApiClient.GetAccessAsync();
        _isTeacher = access?.IsTeacher == true;
        _isLoadingAccess = false;

        if (!_isTeacher)
        {
            _isLoading = false;
            return;
        }

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

    protected async Task GenerateScenarioAsync()
    {
        if (!_isTeacher || _isGenerating)
        {
            return;
        }

        var topic = (GenerateTopic ?? "").Trim();
        var scenarioId = (GenerateScenarioId ?? "").Trim();

        if (string.IsNullOrWhiteSpace(topic))
        {
            SetGenerationMessage("Enter a topic before generating a scenario.", "error");
            return;
        }

        if (!string.IsNullOrWhiteSpace(scenarioId) && !ScenarioIdPattern.IsMatch(scenarioId))
        {
            SetGenerationMessage("Scenario id may only include letters, numbers, underscores, and hyphens.", "error");
            return;
        }

        _isGenerating = true;
        _generationMessage = "Generating a new branching scenario. This can take a few seconds.";
        _generationMessageKind = "info";
        _errorMessage = null;
        StateHasChanged();

        var (scenario, errorMessage) = await ApiClient.GenerateScenarioAsync(topic, scenarioId, GenerateMaxDepth);

        _isGenerating = false;

        if (scenario is null)
        {
            SetGenerationMessage(
                string.IsNullOrWhiteSpace(errorMessage)
                    ? "Scenario generation failed."
                    : errorMessage,
                "error");
            return;
        }

        _scenario = scenario;
        SelectedScenarioId = scenario.Id;
        GenerateScenarioId = scenario.Id;
        _nodeSelection.Clear();
        AddScenarioIdIfMissing(scenario.Id);

        SetGenerationMessage($"Generated and saved '{scenario.Id}'. Review and refine it below.", "success");
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
            SetSaveMessage($"Validation failed: {validationError}", false);
            return;
        }

        _isSaving = true;
        _saveMessage = null;
        StateHasChanged();

        var success = await ApiClient.UpdateScenarioAsync(SelectedScenarioId, _scenario);

        SetSaveMessage(success ? "Scenario saved successfully!" : "Failed to save scenario.", success);
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

    private void SetSaveMessage(string message, bool success)
    {
        _saveSuccess = success;
        _saveMessage = message;
        var version = ++_saveMessageVersion;
        _ = ClearSaveMessageLater(version);
    }

    protected string GetGenerationMessageClass()
    {
        return _generationMessageKind switch
        {
            "success" => "saveMessage success generatorStatus",
            "error" => "saveMessage error generatorStatus",
            _ => "infoMessage generatorStatus"
        };
    }

    private void SetGenerationMessage(string message, string kind)
    {
        _generationMessage = message;
        _generationMessageKind = kind;
    }

    private void AddScenarioIdIfMissing(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            return;
        }

        if (AvailableScenarioIds.Contains(scenarioId, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        AvailableScenarioIds.Add(scenarioId);
        AvailableScenarioIds = AvailableScenarioIds
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task ClearSaveMessageLater(int version)
    {
        await Task.Delay(3000);
        if (version != _saveMessageVersion)
        {
            return;
        }

        _saveMessage = null;
        await InvokeAsync(StateHasChanged);
    }
}
