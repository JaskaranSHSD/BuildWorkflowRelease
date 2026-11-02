namespace BuildWorkflowRelease.Models.Entities;

public class BuildConfig
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int RepositoryId { get; set; }
    public RepositoryConfig? Repository { get; set; }
    public string BuildCommand { get; set; } = "dotnet build";
    public string? WorkingDirectory { get; set; }
    public string? OutputPath { get; set; }
    public string? ArtifactGlob { get; set; } = "**/*";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}