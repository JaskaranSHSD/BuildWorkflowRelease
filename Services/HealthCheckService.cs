using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace BuildWorkflowRelease.Services;

public class HealthCheckService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SftpService _sftp;

    public HealthCheckService(IServiceScopeFactory scopeFactory, SftpService sftp)
    {
        _scopeFactory = scopeFactory;
        _sftp = sftp;
    }

    public async Task<(bool Healthy, string Message)> RunAsync(ApiHealthCheck check, CancellationToken ct = default)
    {
        bool healthy;
        string message;
        int? statusCode = null;
        string? snippet = null;
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        static bool IsHttpUrl(string? value) =>
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        try
        {
            if (!IsHttpUrl(check.Url))
            {
                throw new InvalidOperationException("Health check URL must be an absolute http/https URL.");
            }
            var resp = await http.GetAsync(check.Url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            statusCode = (int)resp.StatusCode;
            snippet = body.Length > 500 ? body.Substring(0, 500) : body;
            var expected = string.IsNullOrWhiteSpace(check.ExpectedResponse) ? "OK" : check.ExpectedResponse!;
            if (resp.IsSuccessStatusCode && body.Contains(expected, StringComparison.OrdinalIgnoreCase))
            {
                healthy = true;
                message = $"OK ({(int)resp.StatusCode})";
            }
            else
            {
                healthy = false;
                message = $"Mismatch: HTTP {(int)resp.StatusCode}, response: {body.Trim()}";
            }
        }
        catch (Exception ex)
        {
            healthy = false;
            message = ex.Message;
        }

        // Recovery actions on failure
        if (!healthy)
        {
            if (!string.IsNullOrWhiteSpace(check.RestartUrl))
            {
                if (!IsHttpUrl(check.RestartUrl))
                {
                    message += "; Restart skipped: invalid URL";
                }
                else
                {
                    try { var r = await http.GetAsync(check.RestartUrl, ct); message += $"; Restart -> {(int)r.StatusCode}"; }
                    catch (Exception ex) { message += $"; Restart failed: {ex.Message}"; }
                }
            }
            if (!string.IsNullOrWhiteSpace(check.NotifyUrl))
            {
                if (!IsHttpUrl(check.NotifyUrl))
                {
                    message += "; Notify skipped: invalid URL";
                }
                else
                {
                    try { var r = await http.GetAsync(check.NotifyUrl, ct); message += $"; Notify -> {(int)r.StatusCode}"; }
                    catch (Exception ex) { message += $"; Notify failed: {ex.Message}"; }
                }
            }
            if (check.SftpConfig != null && !string.IsNullOrWhiteSpace(check.FailureCommands))
            {
                foreach (var cmd in check.FailureCommands.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var res = await _sftp.RunRemoteCommandAsync(check.SftpConfig, cmd, _ => { }, ct);
                    if (!res.Success) message += $"; SFTP '{cmd}' failed: {res.Error}";
                    else message += $"; SFTP '{cmd}' ok";
                }
            }
        }

        // Persist status + log
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tracked = await db.ApiHealthChecks.FindAsync(check.Id);
        if (tracked != null)
        {
            tracked.LastCheckedAt = DateTime.UtcNow;
            tracked.LastHealthy = healthy;
            tracked.LastError = healthy ? null : message;
            await db.SaveChangesAsync(ct);
        }
        db.HealthCheckLogs.Add(new HealthCheckLog
        {
            ApiHealthCheckId = check.Id,
            CheckedAt = DateTime.UtcNow,
            Healthy = healthy,
            StatusCode = statusCode,
            ResponseSnippet = snippet,
            Message = message
        });
        await db.SaveChangesAsync(ct);
        return (healthy, message);
    }
}