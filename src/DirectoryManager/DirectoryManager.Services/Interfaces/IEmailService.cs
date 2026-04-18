namespace DirectoryManager.Services.Interfaces
{
    public interface IEmailService
    {
        Task SendEmailAsync(string subject, string plainTextContent, string htmlContent, List<string> recipients);
    }
}