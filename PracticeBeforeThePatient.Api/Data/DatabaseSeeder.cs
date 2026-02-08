using PracticeBeforeThePatient.Core.Models;

namespace PracticeBeforeThePatient.Api.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // Check if data already exists
        if (context.Scenarios.Any())
        {
            return; // Database has been seeded
        }

        // Create nodes for a test scenario
        var rootNode = new Node
        {
            Type = "mcq",
            Prompt = "A 45-year-old patient presents with chest pain. What is your first action?",
            End = false,
            Choices = new List<Choice>()
        };

        var outcomeNode1 = new Node
        {
            Type = "outcome",
            Prompt = "Good job! You correctly assessed the patient's vital signs first.",
            InfoJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "score", 10 },
                { "feedback", "Excellent clinical judgment" }
            }),
            End = true,
            Choices = new List<Choice>()
        };

        var outcomeNode2 = new Node
        {
            Type = "outcome",
            Prompt = "Incorrect. Always check vital signs before ordering tests.",
            InfoJson = System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object>
            {
                { "score", 0 },
                { "feedback", "Review triage protocols" }
            }),
            End = true,
            Choices = new List<Choice>()
        };

        // Add nodes to context first to generate IDs
        context.Nodes.AddRange(rootNode, outcomeNode1, outcomeNode2);
        await context.SaveChangesAsync();

        // Now create choices with proper NodeIds
        var choice1 = new Choice
        {
            Label = "A",
            Text = "Check vital signs",
            IsCorrect = true,
            Feedback = "Correct! Vital signs are the priority.",
            NodeId = rootNode.Id,
            NextNodeId = outcomeNode1.Id
        };

        var choice2 = new Choice
        {
            Label = "B",
            Text = "Order an EKG immediately",
            IsCorrect = false,
            Feedback = "Not the first step. Always assess the patient first.",
            NodeId = rootNode.Id,
            NextNodeId = outcomeNode2.Id
        };

        context.Choices.AddRange(choice1, choice2);
        await context.SaveChangesAsync();

        // Create scenario
        var scenario = new Scenario
        {
            Title = "Chest Pain Assessment",
            RootNodeId = rootNode.Id
        };

        context.Scenarios.Add(scenario);
        await context.SaveChangesAsync();

        Console.WriteLine("Database seeded successfully!");
    }
}