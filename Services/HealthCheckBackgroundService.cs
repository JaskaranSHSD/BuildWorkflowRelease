using BuildWorkflowRelease.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace BuildWorkflowRelease.Services;

public class HealthCheckBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HealthCheckService _health;

    public HealthCheckBackgroundService(IServiceScopeFactory scopeFactory, HealthCheckService health)
    {
        _scopeFactory = scopeFactory;
        _health = health;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;
                var due = await db.ApiHealthChecks
                    .Where(c => c.Enabled)
                    .ToListAsync(stoppingToken);

                foreach (var check in due)
                {
                    if (!check.Enabled) continue;
                    if (check.LastCheckedAt != null &&
                        check.LastCheckedAt.Value.AddMinutes(check.IntervalMinutes > 0 ? check.IntervalMinutes : 5) > now)
                        continue;

                    await _health.RunAsync(check, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Health background error: " + ex.Message);
            }
            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch (TaskCanceledException) { }
        }
    }
}