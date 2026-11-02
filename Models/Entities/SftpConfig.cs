namespace BuildWorkflowRelease.Models.Entities;

public class SftpConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? PasswordEncrypted { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphraseEncrypted { get; set; }
    public string RemotePath { get; set; } = "/";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}