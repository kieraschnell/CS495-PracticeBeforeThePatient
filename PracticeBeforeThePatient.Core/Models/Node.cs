using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PracticeBeforeThePatient.Core.Models;

public class Node
{
    public string Type { get; set; } = ""; // "mcq" | "outcome" | "info"
    public string? Prompt { get; set; }
    public Dictionary<string, object>? Info { get; set; }
    public List<Choice>? Choices { get; set; }
    public bool End { get; set; } = false;
}
