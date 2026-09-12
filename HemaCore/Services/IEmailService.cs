namespace HemaCore.Services
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(
           string email,
           string name,
           string surname,
           string temporaryPassword,
           string role);
    }
}
