using Microsoft.AspNetCore.Components;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Web.Services;

namespace PracticeBeforeThePatient.Web.Components.Pages;

public partial class Simulation : ComponentBase
{
    [Inject] private ApiClient ApiClient { get; set; } = default!;

    protected Scenario? _scenario;
    protected bool _isLoading = true;
    protected bool _isLoadingScenario;
    protected string? _errorMessage;

    protected List<string> AvailableScenarioIds { get; set; } = new();
    protected string SelectedScenarioId { get; set; } = "";

    protected bool IsScenarioMenuOpen { get; set; }

    protected Node? CurrentDecisionNode { get; set; }

    protected string SelectedOptionLabel { get; set; } = "";
    protected string ReasoningText { get; set; } = "";
    protected bool HasSubmittedThisStep;
    protected string LastResponseText { get; set; } = "";

    protected List<string> CurrentNarration { get; set; } = new();
    protected bool IsComplete;

    protected string EndSummary { get; set; } = "";

    protected List<ComparisonRow> ComparisonRows { get; set; } = new();

    private readonly List<StudentStepRecord> _studentHistory = new();
    private Choice? _selectedChoice;
    private Node? _pendingNode;
    private int _decisionCount;
    private ElementReference _scenarioSelectRef;
    private bool _openSelectAfterRender;

    protected string CurrentStepTitle => $"Step {_decisionCount + 1}";
    protected string CurrentPrompt => CurrentDecisionNode?.Prompt ?? "";
    protected IReadOnlyList<Choice> CurrentChoices => CurrentDecisionNode?.Choices ?? new List<Choice>();

    protected bool CanSubmit =>
        !IsComplete &&
        !HasSubmittedThisStep &&
        !string.IsNullOrWhiteSpace(SelectedOptionLabel) &&
        !string.IsNullOrWhiteSpace(ReasoningText);

    protected bool CanContinue => HasSubmittedThisStep && !IsComplete;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _errorMessage = null;

            var access = await ApiClient.GetAccessAsync();

            if (access == null)
            {
                _errorMessage = "Could not determine access right now.";
                _isLoading = false;
                return;
            }

            AvailableScenarioIds = (access.AllowedScenarioIds ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (AvailableScenarioIds.Count == 0)
            {
                _errorMessage = access.IsAdmin
                    ? "No scenarios are available right now."
                    : "You do not have access to any scenarios right now.";

                _isLoading = false;
                return;
            }

            if (string.IsNullOrWhiteSpace(SelectedScenarioId) || !AvailableScenarioIds.Contains(SelectedScenarioId, StringComparer.OrdinalIgnoreCase))
            {
                SelectedScenarioId = AvailableScenarioIds[0];
            }

            await LoadScenarioAsync(SelectedScenarioId);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load scenarios. {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    protected async Task OnScenarioChanged()
    {
        IsScenarioMenuOpen = false;

        if (!AvailableScenarioIds.Contains(SelectedScenarioId, StringComparer.OrdinalIgnoreCase))
        {
            _errorMessage = "That scenario is not available to you.";
            return;
        }

        await LoadScenarioAsync(SelectedScenarioId);
    }

    protected void ToggleScenarioMenu()
    {
        IsScenarioMenuOpen = !IsScenarioMenuOpen;
    }

    private async Task LoadScenarioAsync(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            _errorMessage = "Select a scenario to continue.";
            return;
        }

        if (!AvailableScenarioIds.Contains(scenarioId, StringComparer.OrdinalIgnoreCase))
        {
            _errorMessage = "That scenario is not available to you.";
            return;
        }

        try
        {
            _errorMessage = null;
            _isLoadingScenario = true;

            _scenario = await ApiClient.GetScenarioAsync(scenarioId);

            if (_scenario == null)
            {
                _errorMessage = "Scenario could not be loaded.";
                return;
            }

            ResetScenarioState();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load scenario. {ex.Message}";
        }
        finally
        {
            _isLoadingScenario = false;
        }
    }

    private void ResetScenarioState()
    {
        CurrentNarration.Clear();
        ComparisonRows.Clear();
        _studentHistory.Clear();

        _decisionCount = 0;
        IsComplete = false;
        HasSubmittedThisStep = false;
        SelectedOptionLabel = "";
        ReasoningText = "";
        LastResponseText = "";
        _pendingNode = null;
        _selectedChoice = null;

        if (_scenario?.Root != null)
        {
            AddNarrationFromNode(_scenario.Root);
            CurrentDecisionNode = _scenario.Root;
        }
        else
        {
            CurrentDecisionNode = null;
            _errorMessage = "Scenario is missing a root node.";
        }
    }

    protected void SelectOption(string label)
    {
        if (HasSubmittedThisStep) return;
        SelectedOptionLabel = label;
    }

    protected void SubmitStep()
    {
        if (CurrentDecisionNode == null) return;

        var choice = CurrentChoices.FirstOrDefault(c => c.Label == SelectedOptionLabel);
        if (choice == null) return;

        _selectedChoice = choice;
        _pendingNode = _selectedChoice.Next;

        _studentHistory.Add(new StudentStepRecord
        {
            StepTitle = CurrentStepTitle,
            DecisionNode = CurrentDecisionNode,
            ChosenChoice = _selectedChoice,
            Reasoning = ReasoningText
        });

        LastResponseText = _pendingNode?.Prompt ?? "";
        HasSubmittedThisStep = true;
    }

    protected void Continue()
    {
        if (_pendingNode == null || _pendingNode.Type == "outcome")
        {
            FinishScenario("Encounter complete.");
            return;
        }

        CurrentDecisionNode = _pendingNode;
        _pendingNode = null;

        AddNarrationFromNode(CurrentDecisionNode);

        SelectedOptionLabel = "";
        ReasoningText = "";
        HasSubmittedThisStep = false;
        _decisionCount++;
    }

    private void FinishScenario(string summary)
    {
        IsComplete = true;
        EndSummary = summary;
        BuildComparison();
    }

    private void AddNarrationFromNode(Node node)
    {
        if (node.Info?.TryGetValue("narration", out var value) == true && value != null)
        {
            CurrentNarration.Add(value.ToString() ?? "");
        }
    }

    private void BuildComparison()
    {
        foreach (var record in _studentHistory)
        {
            var correct = record.DecisionNode.Choices?.FirstOrDefault(c => c.IsCorrect);

            ComparisonRows.Add(new ComparisonRow
            {
                StepTitle = record.StepTitle,
                StudentChoice = $"{record.ChosenChoice.Label}. {record.ChosenChoice.Text}",
                StudentReasoning = record.Reasoning,
                RecommendedChoice = correct != null ? $"{correct.Label}. {correct.Text}" : "No correct option defined for this step.",
                RecommendedWhy = correct?.Feedback ?? "",
                Result = record.ChosenChoice.Feedback ?? ""
            });
        }
    }

    protected void OpenScenarioMenu()
    {
        IsScenarioMenuOpen = true;
        _openSelectAfterRender = true;
    }

    protected void CloseScenarioMenu()
    {
        IsScenarioMenuOpen = false;
    }

    protected sealed class StudentStepRecord
    {
        public string StepTitle { get; set; } = "";
        public Node DecisionNode { get; set; } = default!;
        public Choice ChosenChoice { get; set; } = default!;
        public string Reasoning { get; set; } = "";
    }

    protected sealed class ComparisonRow
    {
        public string StepTitle { get; set; } = "";
        public string StudentChoice { get; set; } = "";
        public string StudentReasoning { get; set; } = "";
        public string RecommendedChoice { get; set; } = "";
        public string RecommendedWhy { get; set; } = "";
        public string Result { get; set; } = "";
    }
}
