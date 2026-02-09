using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PracticeBeforeThePatient.Core.Models;

public class Choice
{
    public string Label { get; set; } = "";
    public string Text { get; set; } = "";
    public bool IsCorrect { get; set; }
    public string? Feedback { get; set; }
    public Node? Next { get; set; }
}