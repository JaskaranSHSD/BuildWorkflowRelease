namespace BuildWorkflowRelease.Models.Entities;

public class HealthCheckLog
{
    public int Id { get; set; }
    public int ApiHealthCheckId { get; set; }
    public ApiHealthCheck? ApiHealthCheck { get; set; }
    public DateTime CheckedAt { get; set; } = DateTime.UtcNow;
    public bool Healthy { get; set; }
    public int? StatusCode { get; set; }
    public string? ResponseSnippet { get; set; }
    public string? Message { get; set; }
}