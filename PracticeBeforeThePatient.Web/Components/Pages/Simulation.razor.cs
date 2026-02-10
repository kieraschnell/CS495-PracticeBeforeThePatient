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

    protected Node? CurrentDecisionNode { get; set; }

    protected string SelectedOptionLabel { get; set; } = "";
    protected string ReasoningText { get; set; } = "";
    protected bool HasSubmittedThisStep { get; set; }
    protected string LastResponseText { get; set; } = "";

    protected List<string> CurrentNarration { get; set; } = new();

    protected bool IsComplete { get; set; }

    protected string EndSummary { get; set; } =
        "You completed the encounter. Review how your decisions compared to the recommended sequence.";

    protected List<ComparisonRow> ComparisonRows { get; set; } = new();

    private readonly List<StudentStepRecord> _studentHistory = new();

    private Choice? _selectedChoice;
    private Node? _pendingNode;
    private int _decisionCount;

    protected string CurrentStepTitle => $"Step {_decisionCount + 1}";

    protected string CurrentPrompt => CurrentDecisionNode?.Prompt ?? "";

    protected IReadOnlyList<Choice> CurrentChoices =>
        CurrentDecisionNode?.Choices ?? new List<Choice>();


    protected bool CanSubmit =>
        !IsComplete &&
        !HasSubmittedThisStep &&
        !string.IsNullOrWhiteSpace(SelectedOptionLabel) &&
        !string.IsNullOrWhiteSpace(ReasoningText) &&
        CurrentChoices.Count > 0;

    protected bool CanContinue =>
        HasSubmittedThisStep && !IsComplete;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _scenario = await ApiClient.GetScenarioAsync("testScenario");

            if (_scenario == null)
            {
                _errorMessage = "Scenario not found or returned null.";
                return;
            }

            if (_scenario.Root == null)
            {
                _errorMessage = "Scenario root node was null.";
                return;
            }

            CurrentNarration = new List<string>();
            AddNarrationFromNode(_scenario.Root);

            var firstDecision = FindNextDecisionNode(_scenario.Root);
            if (firstDecision == null)
            {
                IsComplete = true;
                EndSummary = "This scenario has no decision steps.";
                return;
            }

            CurrentDecisionNode = firstDecision;
            IsComplete = false;
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

    protected void SelectOption(string label)
    {
        if (HasSubmittedThisStep) return;
        SelectedOptionLabel = label;
    }

    protected void SubmitStep()
    {
        if (!CanSubmit || CurrentDecisionNode == null) return;

        var choice = CurrentChoices.FirstOrDefault(c => c.Label == SelectedOptionLabel);
        if (choice == null) return;

        _selectedChoice = choice;
        _pendingNode = choice.Next;

        _studentHistory.Add(new StudentStepRecord
        {
            StepTitle = CurrentStepTitle,
            ChosenLabel = choice.Label,
            ChosenText = $"{choice.Label}. {choice.Text}",
            Reasoning = ReasoningText,
            IsCorrect = choice.IsCorrect,
            Feedback = choice.Feedback ?? ""
        });

        LastResponseText = _pendingNode?.Prompt ?? "The patient waits for your next action.";
        HasSubmittedThisStep = true;
    }

    protected void Continue()
    {
        if (!HasSubmittedThisStep) return;

        SelectedOptionLabel = "";
        ReasoningText = "";
        HasSubmittedThisStep = false;

        if (_pendingNode == null)
        {
            FinishScenario("The scenario ended unexpectedly because the next node was missing.");
            return;
        }

        var node = _pendingNode;
        _pendingNode = null;

        AddNarrationFromNode(node);

        if (node.End)
        {
            FinishScenario("Encounter complete. The patient is transitioned to definitive care.");
            return;
        }

        var nextDecision = FindNextDecisionNode(node);
        if (nextDecision == null)
        {
            FinishScenario("Encounter complete. No further decision steps were provided.");
            return;
        }

        CurrentDecisionNode = nextDecision;
        LastResponseText = "";
        _decisionCount++;
    }

    private void FinishScenario(string summary)
    {
        IsComplete = true;
        EndSummary = summary;
        BuildComparison();
    }

    private static Node? FindNextDecisionNode(Node start)
    {
        var current = start;

        while (true)
        {
            if (current.Choices != null && current.Choices.Count > 0)
            {
                return current;
            }

            if (current.End)
            {
                return null;
            }

            return null;
        }
    }

    private void AddNarrationFromNode(Node node)
    {
        if (node.Info == null) return;

        if (node.Info.TryGetValue("narration", out var obj) && obj != null)
        {
            var text = obj.ToString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                CurrentNarration.Add(text);
            }
        }
    }

    private void BuildComparison()
    {
        ComparisonRows = new List<ComparisonRow>();

        foreach (var record in _studentHistory)
        {
            var recommended = record.IsCorrect ? record.ChosenText : "Correct option not provided by API for this step.";
            var recommendedWhy = record.IsCorrect ? record.Feedback : "Add a node level explanation or a dedicated rationale on the correct choice to power this column.";

            ComparisonRows.Add(new ComparisonRow
            {
                StepTitle = record.StepTitle,
                StudentChoice = record.ChosenText,
                StudentReasoning = record.Reasoning,
                RecommendedChoice = recommended,
                RecommendedWhy = recommendedWhy,
                Result = record.Feedback
            });
        }
    }

    private sealed class StudentStepRecord
    {
        public string StepTitle { get; set; } = "";
        public string ChosenLabel { get; set; } = "";
        public string ChosenText { get; set; } = "";
        public string Reasoning { get; set; } = "";
        public bool IsCorrect { get; set; }
        public string Feedback { get; set; } = "";
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
