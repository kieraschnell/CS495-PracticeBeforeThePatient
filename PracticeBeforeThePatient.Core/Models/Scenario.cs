using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace PracticeBeforeThePatient.Core.Models;

public class Scenario
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [Required]
    public string Title { get; set; } = "";

    // Store the entire scenario tree as JSON in the database
    public string ScenarioJson { get; set; } = "";

    // Not mapped to database - for backward compatibility with existing code
    [NotMapped]
    public Node Root
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ScenarioJson))
                return new Node();

            try
            {
                var data = JsonSerializer.Deserialize<ScenarioData>(ScenarioJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                return data?.Root ?? new Node();
            }
            catch
            {
                return new Node();
            }
        }
        set
        {
            var data = new ScenarioData { Root = value };
            ScenarioJson = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });
        }
    }

    // Helper class for JSON serialization
    private class ScenarioData
    {
        public Node Root { get; set; } = new();
    }
}
