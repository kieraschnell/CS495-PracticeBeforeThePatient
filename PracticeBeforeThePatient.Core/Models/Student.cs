using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PracticeBeforeThePatient.Core.Models;

public class Student
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    
    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";
    
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    
    // Foreign key to Class
    public string? ClassId { get; set; }
    public Class? Class { get; set; }
}
