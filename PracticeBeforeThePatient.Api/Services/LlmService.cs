using System.Text.Json;
using PracticeBeforeThePatient.Core.Models;

namespace PracticeBeforeThePatient.Services;

public sealed class LlmService
{
    private static readonly JsonSerializerOptions DeserializeOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonSerializerOptions SerializeOptions = new() { PropertyNamingPolicy = null };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _provider;
    private readonly string _apiKey;
    private readonly string _model;

    public LlmService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _provider = configuration["Llm:Provider"] ?? "gemini";
        _apiKey = configuration["Llm:ApiKey"] ?? "";
        _model = configuration["Llm:Model"] ?? "gemini-2.5-flash";
    }

    public async Task<Scenario?> GenerateScenarioAsync(string topic, int maxDepth = 2, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("Llm:ApiKey is not configured.");

        return _provider.ToLowerInvariant() switch
        {
            "gemini" => await GenerateViaGeminiAsync(topic, maxDepth, ct),
            _ => throw new InvalidOperationException($"Unsupported LLM provider: {_provider}")
        };
    }

    private async Task<Scenario?> GenerateViaGeminiAsync(string topic, int maxDepth, CancellationToken ct)
    {
        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = BuildPrompt(topic, maxDepth) } } } },
            generationConfig = new
            {
                temperature = 0.7,
                responseMimeType = "application/json",
                responseSchema = BuildGeminiSchema(maxDepth)
            }
        };

        var client = _httpClientFactory.CreateClient();
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        var response = await client.PostAsJsonAsync(url, requestBody, SerializeOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"Gemini returned {(int)response.StatusCode}: {errorBody}");
        }

        var responseText = await response.Content.ReadAsStringAsync(ct);
        var scenarioJson = ExtractGeminiJson(responseText);
        return scenarioJson is null ? null : JsonSerializer.Deserialize<Scenario>(scenarioJson, DeserializeOptions);
    }

    private static string? ExtractGeminiJson(string response)
    {
        using var doc = JsonDocument.Parse(response);
        if (!doc.RootElement.TryGetProperty("candidates", out var candidates)
            || candidates.GetArrayLength() == 0)
            return null;

        return candidates[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();
    }

    // Recursively builds a depth-limited JSON Schema for Gemini's responseSchema.
    // At each level a node can be outcome (terminal) or MCQ (branching deeper).
    // At depth 0 only outcomes are allowed. Root is always MCQ with Info.
    private static object BuildGeminiSchema(int maxDepth)
    {
        return new
        {
            type = "object",
            properties = new
            {
                Title = new { type = "string" },
                Root = (object)BuildNodeSchema(maxDepth, isRoot: true)
            },
            required = new[] { "Title", "Root" }
        };
    }

    private static object BuildNodeSchema(int remainingDepth, bool isRoot = false)
    {
        if (remainingDepth <= 0)
        {
            return new
            {
                type = "object",
                properties = new
                {
                    Type = new { type = "string", @enum = new[] { "outcome" } },
                    Prompt = new { type = "string" }
                },
                required = new[] { "Type", "Prompt" }
            };
        }

        var choiceSchema = BuildChoiceSchema(remainingDepth - 1);

        if (isRoot)
        {
            return new
            {
                type = "object",
                properties = new
                {
                    Type = new { type = "string", @enum = new[] { "mcq" } },
                    Prompt = new { type = "string" },
                    Info = new
                    {
                        type = "object",
                        properties = new { narration = new { type = "string" } },
                        required = new[] { "narration" }
                    },
                    Choices = new { type = "array", items = (object)choiceSchema }
                },
                required = new[] { "Type", "Prompt", "Info", "Choices" }
            };
        }

        // Non-root: can be MCQ or outcome. Choices optional (present for MCQ, absent for outcome).
        return new
        {
            type = "object",
            properties = new
            {
                Type = new { type = "string", @enum = new[] { "mcq", "outcome" } },
                Prompt = new { type = "string" },
                Choices = new { type = "array", items = (object)choiceSchema }
            },
            required = new[] { "Type", "Prompt" }
        };
    }

    private static object BuildChoiceSchema(int nextDepth)
    {
        return new
        {
            type = "object",
            properties = new
            {
                Label = new { type = "string" },
                Text = new { type = "string" },
                IsCorrect = new { type = "boolean" },
                Feedback = new { type = "string" },
                Next = (object)BuildNodeSchema(nextDepth)
            },
            required = new[] { "Label", "Text", "IsCorrect", "Feedback", "Next" }
        };
    }

    private static string BuildPrompt(string topic, int maxDepth) => $"""
        Generate a branching athletic training clinical scenario on the topic: {topic}.

        The scenario tree should have up to {maxDepth} levels of multiple-choice questions.
        Not all branches need the same depth — some paths can reach an outcome sooner than others.
        Clinical decisions must follow evidence-based sports medicine practice.
        The correct decision path should lead to a good patient outcome.
        Incorrect choices should lead to realistic negative outcomes.
        Include realistic scene-setting context in the root node's narration.
        """;
}
