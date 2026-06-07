namespace Rava.Api.Services;

public class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "noreply@rava.local";
    public string FromName { get; set; } = "theexonet";
    public string AppBaseUrl { get; set; } = "http://localhost:5000";
    public bool UseStartTls { get; set; } = true;
    public bool UseSsl { get; set; }
}
