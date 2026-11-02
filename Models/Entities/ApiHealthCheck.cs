namespace BuildWorkflowRelease.Models.Entities;

public class ApiHealthCheck
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? ExpectedResponse { get; set; } = "OK";
    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;

    public string? RestartUrl { get; set; }
    public string? NotifyUrl { get; set; }

    // Optional SFTP/SSH target + commands to run when check fails
    public int? SftpConfigId { get; set; }
    public SftpConfig? SftpConfig { get; set; }
    public string? FailureCommands { get; set; }

    public DateTime? LastCheckedAt { get; set; }
    public bool LastHealthy { get; set; }
    public string? LastError { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}