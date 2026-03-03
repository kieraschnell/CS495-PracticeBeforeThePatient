using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PracticeBeforeThePatient.Core.Models;

public class Class
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    [Required]
    public string Name { get; set; } = "";
    
    // Foreign key to Instructor
    public string? InstructorId { get; set; }
    public Instructor? Instructor { get; set; }
    
    public List<Student> Students { get; set; } = new();
    public List<string> AllowedScenarioIds { get; set; } = new();
}
