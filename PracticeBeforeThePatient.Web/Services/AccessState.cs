namespace PracticeBeforeThePatient.Web.Services;

public sealed class AccessState
{
    public ApiClient.AccessResponse? CurrentAccess { get; private set; }

    public event Action? Changed;

    public void Update(ApiClient.AccessResponse? access)
    {
        CurrentAccess = access;
        Changed?.Invoke();
    }
}
