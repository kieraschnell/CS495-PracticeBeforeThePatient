using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace PracticeBeforeThePatient.Core.Models;

public class Node
{
    public int Id { get; set; } // Add primary key for EF Core

    public string Type { get; set; } = ""; // "mcq" | "outcome" | "info"
    public string? Prompt { get; set; }

    // Store the dictionary as JSON in the database
    public string? InfoJson { get; set; }

    [NotMapped]
    public Dictionary<string, object>? Info
    {
        get => string.IsNullOrEmpty(InfoJson)
            ? null
            : JsonSerializer.Deserialize<Dictionary<string, object>>(InfoJson);
        set => InfoJson = value == null
            ? null
            : JsonSerializer.Serialize(value);
    }

    public List<Choice> Choices { get; set; } = new();
    public bool End { get; set; } = false;
}