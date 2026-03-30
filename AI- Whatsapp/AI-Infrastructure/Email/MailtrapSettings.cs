namespace EcomAI.Platform.Infrastructure.Email;

public sealed class MailtrapSettings
{
    public string Host { get; set; } = "sandbox.smtp.mailtrap.io";
    public int Port { get; set; } = 2525;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = "noreply@autobaat.io";
    public string FromName { get; set; } = "AutoBaat Platform";
}
