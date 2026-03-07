namespace PracticeBeforeThePatient.Data.Entities;

public class UserEntity
{
    public int Id { get; set; }
    public string SsoSubject { get; set; } = "";
    public string Email { get; set; } = "";
    public string Name { get; set; } = "";
    public string Role { get; set; } = "student";
}
