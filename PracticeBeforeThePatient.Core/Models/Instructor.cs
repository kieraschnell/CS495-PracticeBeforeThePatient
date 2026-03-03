using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PracticeBeforeThePatient.Core.Models;

public class Instructor
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
    
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    
    public List<Class> Classes { get; set; } = new();
}
