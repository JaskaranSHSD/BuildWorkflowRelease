namespace BuildWorkflowRelease.Models.Entities;

public class ReleaseProfile
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int BuildConfigId { get; set; }
    public BuildConfig? BuildConfig { get; set; }
    public int SftpConfigId { get; set; }
    public SftpConfig? SftpConfig { get; set; }
    public bool AutoPull { get; set; } = true;
    public bool AutoBuild { get; set; } = true;
    public bool AutoUpload { get; set; } = true;
    public string? PostRunCommands { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ReleaseRun
{
    public int Id { get; set; }
    public int ReleaseProfileId { get; set; }
    public ReleaseProfile? ReleaseProfile { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Pulling, Building, Uploading, Completed, Failed
    public string? CurrentStep { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public ICollection<ReleaseRunLog> Logs { get; set; } = new List<ReleaseRunLog>();
}

public class ReleaseRunLog
{
    public int Id { get; set; }
    public int ReleaseRunId { get; set; }
    public ReleaseRun? ReleaseRun { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
}