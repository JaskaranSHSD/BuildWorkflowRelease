namespace BuildWorkflowRelease.Models.Entities;

public class RepositoryConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GitUrl { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string LocalPath { get; set; } = string.Empty;
    public string? AccessTokenEncrypted { get; set; }
    public string? Username { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAt { get; set; }
}