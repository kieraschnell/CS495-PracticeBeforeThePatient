using Microsoft.AspNetCore.Components;

namespace PracticeBeforeThePatient.Web.Components.Pages;

public partial class Simulation : ComponentBase
{
    protected string CaseTitle { get; set; } = "Practice Scenario";
    protected string CaseSubtitle { get; set; } = "Make the next best decision at each step";

    protected int StepIndex { get; set; } = 0;

    protected string SelectedOptionId { get; set; } = "";
    protected string ReasoningText { get; set; } = "";

    protected bool HasSubmittedThisStep { get; set; } = false;

    protected string LastResponseText { get; set; } = "";

    protected List<string> CurrentNarration { get; set; } = new();

    protected bool IsComplete => StepIndex >= Steps.Count;

    protected Step CurrentDecision =>
        Steps[Math.Min(StepIndex, Steps.Count - 1)];

    protected bool CanSubmit =>
        !IsComplete &&
        !HasSubmittedThisStep &&
        !string.IsNullOrWhiteSpace(SelectedOptionId) &&
        !string.IsNullOrWhiteSpace(ReasoningText);

    protected bool CanContinue => HasSubmittedThisStep && !IsComplete;

    protected string EndSummary { get; set; } =
        "You completed the encounter. Review how your decisions compared to the recommended sequence.";

    protected List<ComparisonRow> ComparisonRows { get; set; } = new();

    private List<StudentStepRecord> StudentHistory { get; set; } = new();

    protected List<Step> Steps { get; } = new()
    {
        new Step
        {
            Title = "Step 1",
            Prompt = "What would you do next",
            OptimalOptionId = "B",
            Options = new()
            {
                new Option { Id = "A", Key = "A", Text = "Ask what changed right before symptoms started" },
                new Option { Id = "B", Key = "B", Text = "Assess airway and breathing right now" },
                new Option { Id = "C", Key = "C", Text = "Offer water and have them sit upright" },
                new Option { Id = "D", Key = "D", Text = "Ask about past medical history first" }
            },
            PatientResponseByOption = new()
            {
                ["A"] = "I ate a protein bar a few minutes ago. My throat feels tight and I feel lightheaded.",
                ["B"] = "I am breathing fast and I feel like my throat is closing. I feel dizzy.",
                ["C"] = "I can try but I feel nauseated and my throat feels tight.",
                ["D"] = "I have allergies but I have never felt this bad. My throat feels tight."
            },
            OptimalWhy = "Airway and breathing threats can progress quickly. Your first job is to identify immediate instability before anything else.",
            CaseConsequenceByOption = new()
            {
                ["A"] = "You learn the likely trigger, but you have not yet assessed how unstable the patient is right now.",
                ["B"] = "You immediately focus on the time sensitive threat and can escalate quickly if needed.",
                ["C"] = "Oral intake is not appropriate when nausea and airway tightness are present, and it delays assessment.",
                ["D"] = "Background history matters, but delaying airway assessment can miss rapid deterioration."
            }
        },
        new Step
        {
            Title = "Step 2",
            Prompt = "The patient looks worse. What would you do next",
            OptimalOptionId = "B",
            Options = new()
            {
                new Option { Id = "A", Key = "A", Text = "Give oral antihistamine and monitor" },
                new Option { Id = "B", Key = "B", Text = "Activate EMS and give IM epinephrine" },
                new Option { Id = "C", Key = "C", Text = "Give oxygen and reassess before medication" },
                new Option { Id = "D", Key = "D", Text = "Have them use an asthma inhaler" }
            },
            PatientResponseByOption = new()
            {
                ["A"] = "I feel worse. My throat feels tighter and I am more dizzy.",
                ["B"] = "After the injection, it feels a little easier to breathe. I am still scared.",
                ["C"] = "The mask helps a bit, but I still feel like my throat is tight.",
                ["D"] = "It is not helping. I still feel like I cannot breathe right."
            },
            OptimalWhy = "IM epinephrine is first line when you suspect anaphylaxis with airway symptoms or hypotension. Activating EMS early reduces risk if the patient deteriorates.",
            CaseConsequenceByOption = new()
            {
                ["A"] = "Skin symptoms may improve, but airway swelling and low blood pressure are not treated.",
                ["B"] = "The most time sensitive treatment is given and you trigger definitive care.",
                ["C"] = "Oxygen can help saturation but does not treat airway swelling or shock.",
                ["D"] = "It can help bronchospasm but does not treat systemic anaphylaxis."
            }
        },
        new Step
        {
            Title = "Step 3",
            Prompt = "Several minutes later, symptoms return. What would you do next",
            OptimalOptionId = "B",
            Options = new()
            {
                new Option { Id = "A", Key = "A", Text = "Wait for EMS since medication was already given" },
                new Option { Id = "B", Key = "B", Text = "Give a second IM epinephrine dose" },
                new Option { Id = "C", Key = "C", Text = "Give oral fluids for low blood pressure" },
                new Option { Id = "D", Key = "D", Text = "Give an antihistamine only" }
            },
            PatientResponseByOption = new()
            {
                ["A"] = "I feel worse again. My throat is getting tight and I feel weak.",
                ["B"] = "After the second injection, it improves again. I can breathe a little easier.",
                ["C"] = "I feel nauseated and gag when I try to drink.",
                ["D"] = "My skin itching is a little better, but my throat still feels tight."
            },
            OptimalWhy = "Recurring symptoms can require repeat epinephrine before transfer of care. You treat the airway and perfusion threat again, not just the skin symptoms.",
            CaseConsequenceByOption = new()
            {
                ["A"] = "Waiting can allow airway edema and hypotension to worsen again.",
                ["B"] = "Repeating epinephrine can stabilize symptoms while awaiting definitive care.",
                ["C"] = "Oral fluids are unsafe with nausea and airway symptoms and do not treat the underlying reaction.",
                ["D"] = "Antihistamines do not address airway compromise or hypotension."
            }
        }
    };

    protected override void OnInitialized()
    {
        CurrentNarration = new List<string>
        {
            "The patient walks in looking anxious.",
            "They say they feel itchy and their throat feels tight.",
            "They seem short of breath."
        };
    }

    protected void SelectOption(string id)
    {
        if (HasSubmittedThisStep) return;
        SelectedOptionId = id;
    }

    protected void SubmitStep()
    {
        if (!CanSubmit) return;

        var step = CurrentDecision;
        var chosenText = getOptionText(step, SelectedOptionId);

        StudentHistory.Add(new StudentStepRecord
        {
            StepTitle = step.Title,
            ChosenOptionId = SelectedOptionId,
            ChosenOptionText = chosenText,
            Reasoning = ReasoningText
        });

        var response = step.PatientResponseByOption.TryGetValue(SelectedOptionId, out var text)
            ? text
            : "The patient waits for your next action.";

        LastResponseText = response;

        HasSubmittedThisStep = true;
    }

    protected void Continue()
    {
        if (!HasSubmittedThisStep) return;

        StepIndex++;

        SelectedOptionId = "";
        ReasoningText = "";
        HasSubmittedThisStep = false;
        LastResponseText = "";

        if (IsComplete)
        {
            buildEndSummaryAndComparison();
            return;
        }

        advanceNarration();
    }

    private void advanceNarration()
    {
        if (StepIndex == 1)
        {
            CurrentNarration.Add("You notice hives on their skin and their breathing is rapid.");
        }

        if (StepIndex == 2)
        {
            CurrentNarration.Add("They look fatigued. The throat tightness seems to come and go.");
        }
    }

    private void buildEndSummaryAndComparison()
    {
        EndSummary =
            "EMS arrives and the patient is transported for monitoring. The key risk was a rapidly progressing allergic emergency with airway and blood pressure involvement.";

        ComparisonRows = new List<ComparisonRow>();

        for (var i = 0; i < Steps.Count; i++)
        {
            var step = Steps[i];
            var student = StudentHistory.Count > i ? StudentHistory[i] : new StudentStepRecord { StepTitle = step.Title };

            var optimalText = getOptionText(step, step.OptimalOptionId);

            var consequence = step.CaseConsequenceByOption.TryGetValue(student.ChosenOptionId, out var c)
                ? c
                : "No case consequence text is available for that selection.";

            ComparisonRows.Add(new ComparisonRow
            {
                StepTitle = step.Title,
                StudentChoice = string.IsNullOrWhiteSpace(student.ChosenOptionText) ? "No selection recorded." : student.ChosenOptionText,
                StudentReasoning = string.IsNullOrWhiteSpace(student.Reasoning) ? "" : student.Reasoning,
                OptimalChoice = optimalText,
                WhyOptimal = step.OptimalWhy,
                CaseConsequence = consequence
            });
        }
    }

    private static string getOptionText(Step step, string optionId)
    {
        var opt = step.Options.FirstOrDefault(o => o.Id == optionId);
        return opt == null ? "" : $"{opt.Key}. {opt.Text}";
    }

    protected sealed class Step
    {
        public string Title { get; set; } = "";
        public string Prompt { get; set; } = "";
        public string OptimalOptionId { get; set; } = "";
        public string OptimalWhy { get; set; } = "";
        public List<Option> Options { get; set; } = new();
        public Dictionary<string, string> PatientResponseByOption { get; set; } = new();
        public Dictionary<string, string> CaseConsequenceByOption { get; set; } = new();
    }

    protected sealed class Option
    {
        public string Id { get; set; } = "";
        public string Key { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private sealed class StudentStepRecord
    {
        public string StepTitle { get; set; } = "";
        public string ChosenOptionId { get; set; } = "";
        public string ChosenOptionText { get; set; } = "";
        public string Reasoning { get; set; } = "";
    }

    protected sealed class ComparisonRow
    {
        public string StepTitle { get; set; } = "";
        public string StudentChoice { get; set; } = "";
        public string StudentReasoning { get; set; } = "";
        public string OptimalChoice { get; set; } = "";
        public string WhyOptimal { get; set; } = "";
        public string CaseConsequence { get; set; } = "";
    }
}
