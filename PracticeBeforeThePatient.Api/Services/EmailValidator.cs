using System.Net.Mail;

namespace PracticeBeforeThePatient.Services;

public static class EmailValidator
{
    public static bool LooksLikeEmail(string value)
    {
        try
        {
            var addr = new MailAddress(value);
            return string.Equals(addr.Address, value, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
