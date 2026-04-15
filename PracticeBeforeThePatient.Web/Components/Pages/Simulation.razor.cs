using Microsoft.AspNetCore.Components;
using PracticeBeforeThePatient.Core.Models;
using PracticeBeforeThePatient.Web.Services;

namespace PracticeBeforeThePatient.Web.Components.Pages;

public partial class Simulation : ComponentBase
{
    [SupplyParameterFromQuery(Name = "assignment")]
    public string? RequestedAssignmentOptionId { get; set; }

    [Inject] private ApiClient ApiClient { get; set; } = default!;

    protected Scenario? _scenario;
    protected bool _isLoading = true;
    protected bool _isLoadingScenario;
    protected string? _errorMessage;

    protected List<ScenarioSelectionOption> AvailableScenarios { get; set; } = new();
    protected string SelectedScenarioOptionId { get; set; } = "";
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
    protected string AssignmentSubmissionStatus { get; set; } = "";
    protected bool IsGuestMode { get; set; }

    private readonly List<StudentStepRecord> _studentHistory = new();
    private Choice? _selectedChoice;
    private Node? _pendingNode;
    private int _decisionCount;
    private ElementReference _scenarioSelectRef;
    private bool _openSelectAfterRender;

    protected string CurrentStepTitle => $"Step {_decisionCount + 1}";
    protected string CurrentPrompt => CurrentDecisionNode?.Prompt ?? "";
    protected IReadOnlyList<Choice> CurrentChoices => CurrentDecisionNode?.Choices ?? new List<Choice>();
    protected string DownloadResponseFileName => BuildDownloadFileName();
    protected string DownloadResponseHref => $"data:text/plain;charset=utf-8,{Uri.EscapeDataString(BuildSubmissionText())}";

    protected bool CanSubmit =>
        !IsComplete &&
        !HasSubmittedThisStep &&
        !string.IsNullOrWhiteSpace(SelectedOptionLabel) &&
        !string.IsNullOrWhiteSpace(ReasoningText);

    protected bool CanContinue => HasSubmittedThisStep && !IsComplete;
    private HashSet<string> AvailableScenarioIdSet =>
        AvailableScenarios
            .Select(x => x.ScenarioId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> AvailableScenarioOptionIdSet =>
        AvailableScenarios
            .Select(x => x.OptionId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

            ApplyAccessOptions(access);

            if (AvailableScenarios.Count == 0)
            {
                _errorMessage = access.IsAdmin
                    ? "No scenarios are available right now."
                    : "No scenarios are available right now.";
                _isLoading = false;
                return;
            }

            if (!string.IsNullOrWhiteSpace(RequestedAssignmentOptionId) && AvailableScenarioOptionIdSet.Contains(RequestedAssignmentOptionId))
            {
                SelectedScenarioOptionId = RequestedAssignmentOptionId;
            }
            else if (string.IsNullOrWhiteSpace(SelectedScenarioOptionId) || !AvailableScenarioOptionIdSet.Contains(SelectedScenarioOptionId))
            {
                SelectedScenarioOptionId = AvailableScenarios[0].OptionId;
            }

            var selectedOption = GetSelectedScenarioOption();
            SelectedScenarioId = selectedOption?.ScenarioId ?? "";
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

    protected string GetSelectedScenarioDisplayName()
    {
        var selected = GetSelectedScenarioOption();
        return selected?.DisplayName ?? (_scenario?.Title ?? SelectedScenarioId);
    }

    protected string GetActiveScenarioSubtitle()
    {
        if (_scenario is null)
        {
            return "Make the next best decision at each step";
        }

        var selected = GetSelectedScenarioOption();
        if (selected is null)
        {
            return "Make the next best decision at each step";
        }

        if (string.Equals(selected.DisplayName, selected.ScenarioId, StringComparison.OrdinalIgnoreCase))
        {
            return "Make the next best decision at each step";
        }

        return $"{selected.DisplayName} · {_scenario.Title}";
    }

    protected async Task OnScenarioChanged()
    {
        IsScenarioMenuOpen = false;

        if (!AvailableScenarioOptionIdSet.Contains(SelectedScenarioOptionId))
        {
            _errorMessage = "That scenario is not available to you.";
            return;
        }

        var selected = GetSelectedScenarioOption();
        SelectedScenarioId = selected?.ScenarioId ?? "";
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

        if (!AvailableScenarioIdSet.Contains(scenarioId))
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
        AssignmentSubmissionStatus = "";

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

    protected async Task Continue()
    {
        if (_pendingNode == null || _pendingNode.Type == "outcome")
        {
            await FinishScenarioAsync("Encounter complete.");
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

    private async Task FinishScenarioAsync(string summary)
    {
        IsComplete = true;
        EndSummary = summary;
        BuildComparison();
        await SaveAssignmentSubmissionAsync();
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

    private async Task SaveAssignmentSubmissionAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedScenarioId))
        {
            AssignmentSubmissionStatus = "";
            return;
        }

        if (IsGuestMode)
        {
            AssignmentSubmissionStatus = "Practice mode is active. Saving results to a class record is still in progress and is not available in this build yet.";
            return;
        }

        var submissionText = BuildSubmissionText();
        var result = await ApiClient.SubmitScenarioAsync(SelectedScenarioId, submissionText);

        if (result is null)
        {
            AssignmentSubmissionStatus = "Could not save assignment submission.";
            return;
        }

        AssignmentSubmissionStatus = result.UpdatedAssignments > 0
            ? $"Assignment submission saved for {result.UpdatedAssignments} class assignment(s)."
            : "No class assignments were linked to this scenario.";

        await RefreshScenarioOptionsAsync();
    }

    private string BuildSubmissionText()
    {
        var lines = new List<string>
        {
            $"Scenario: {SelectedScenarioId}",
            $"Completed At (UTC): {DateTimeOffset.UtcNow:O}",
            $"Summary: {EndSummary}"
        };

        foreach (var row in ComparisonRows)
        {
            lines.Add("");
            lines.Add(row.StepTitle);
            lines.Add($"Student Choice: {row.StudentChoice}");
            lines.Add($"Reasoning: {row.StudentReasoning}");
            lines.Add($"Recommended: {row.RecommendedChoice}");
            lines.Add($"Recommended Why: {row.RecommendedWhy}");
            lines.Add($"Result: {row.Result}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildDownloadFileName()
    {
        var rawScenarioId = string.IsNullOrWhiteSpace(SelectedScenarioId) ? "simulation-response" : SelectedScenarioId.Trim();
        var safeScenarioId = new string(rawScenarioId
            .Select(ch => char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' ? ch : '-')
            .ToArray())
            .Trim('-');

        if (string.IsNullOrWhiteSpace(safeScenarioId))
        {
            safeScenarioId = "simulation-response";
        }

        return $"{safeScenarioId}-{DateTimeOffset.Now:yyyyMMdd-HHmm}.txt";
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

    private void ApplyAccessOptions(ApiClient.AccessResponse access)
    {
        var nowUtc = DateTimeOffset.UtcNow;
        IsGuestMode = access.IsGuest;

        AvailableScenarios = (access.AllowedScenarioOptions ?? new List<ApiClient.AllowedScenarioOption>())
            .Where(x => !string.IsNullOrWhiteSpace(x.AssignmentId) && !string.IsNullOrWhiteSpace(x.ScenarioId))
            .Where(x => x.AssignedAtUtc <= nowUtc)
            .Where(x => !x.DueAtUtc.HasValue || x.DueAtUtc.Value >= nowUtc)
            .Select(x => new ScenarioSelectionOption
            {
                OptionId = x.AssignmentId.Trim(),
                ScenarioId = x.ScenarioId.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(x.Label) ? x.ScenarioId.Trim() : x.Label.Trim(),
                DueAtUtc = x.DueAtUtc,
                IsSubmitted = x.IsSubmitted
            })
            .OrderBy(x => x.DueAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (AvailableScenarios.Count == 0 && access.IsAdmin)
        {
            AvailableScenarios = (access.AllowedScenarioIds ?? new List<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => new ScenarioSelectionOption
                {
                    OptionId = x.Trim(),
                    ScenarioId = x.Trim(),
                    DisplayName = x.Trim(),
                    DueAtUtc = null,
                    IsSubmitted = false
                })
                .ToList();
        }
    }

    private async Task RefreshScenarioOptionsAsync()
    {
        var access = await ApiClient.GetAccessAsync();
        if (access is null)
        {
            return;
        }

        var previousOptionId = SelectedScenarioOptionId;
        ApplyAccessOptions(access);

        if (!string.IsNullOrWhiteSpace(previousOptionId) && AvailableScenarios.Any(x => string.Equals(x.OptionId, previousOptionId, StringComparison.OrdinalIgnoreCase)))
        {
            SelectedScenarioOptionId = previousOptionId;
            return;
        }

        if (AvailableScenarios.Count > 0)
        {
            SelectedScenarioOptionId = AvailableScenarios[0].OptionId;
            SelectedScenarioId = AvailableScenarios[0].ScenarioId;
        }
    }

    protected string GetScenarioOptionLabel(ScenarioSelectionOption option)
    {
        return option.IsSubmitted
            ? $"{option.DisplayName} (Done)"
            : $"{option.DisplayName} (Not done)";
    }

    private ScenarioSelectionOption? GetSelectedScenarioOption()
    {
        return AvailableScenarios
            .FirstOrDefault(x => string.Equals(x.OptionId, SelectedScenarioOptionId, StringComparison.OrdinalIgnoreCase));
    }

    protected sealed class ScenarioSelectionOption
    {
        public string OptionId { get; set; } = "";
        public string ScenarioId { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public DateTimeOffset? DueAtUtc { get; set; }
        public bool IsSubmitted { get; set; }
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
