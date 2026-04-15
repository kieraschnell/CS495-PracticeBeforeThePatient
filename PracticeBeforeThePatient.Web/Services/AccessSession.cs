namespace PracticeBeforeThePatient.Web.Services;

public sealed class AccessSession
{
    public string SessionId { get; } = Guid.NewGuid().ToString("N");
}
