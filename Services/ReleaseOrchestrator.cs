using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;

namespace BuildWorkflowRelease.Services;

public class ReleaseOrchestrator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GitService _git;
    private readonly BuildService _build;
    private readonly SftpService _sftp;

    public ReleaseOrchestrator(IServiceScopeFactory scopeFactory, GitService git, BuildService build, SftpService sftp)
    {
        _scopeFactory = scopeFactory;
        _git = git;
        _build = build;
        _sftp = sftp;
    }

    public void Start(int profileId, int runId)
    {
        _ = Task.Run(() => ExecuteAsync(profileId, runId, CancellationToken.None));
    }

    private async Task ExecuteAsync(int profileId, int runId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var run = await db.ReleaseRuns.Include(r => r.ReleaseProfile).ThenInclude(p => p!.BuildConfig).ThenInclude(b => b!.Repository).Include(r => r.ReleaseProfile).ThenInclude(p => p!.SftpConfig).FirstOrDefaultAsync(r => r.Id == runId, ct);
        if (run == null) return;
        var profile = run.ReleaseProfile!;
        var buildConfig = profile.BuildConfig!;
        var repo = buildConfig.Repository!;
        var sftpConfig = profile.SftpConfig!;

        Action<string> log = msg => Log(db, run, msg);

        try
        {
            // Pull
            if (profile.AutoPull)
            {
                run.Status = "Pulling"; run.CurrentStep = "Pulling latest changes"; await Save(db, run);
                var pullRes = await _git.CloneOrPullAsync(repo, log, ct);
                if (!pullRes.Success) throw new Exception("Git pull failed: " + pullRes.Output);
                repo.LastSyncedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            // Build
            List<string> artifacts = new();
            if (profile.AutoBuild)
            {
                run.Status = "Building"; run.CurrentStep = "Running build"; await Save(db, run);
                var buildRes = await _build.RunAsync(buildConfig, log, ct);
                if (!buildRes.Success) throw new Exception("Build failed");
                artifacts = buildRes.Artifacts;
            }

            // Upload
            if (profile.AutoUpload)
            {
                run.Status = "Uploading"; run.CurrentStep = "Uploading artifacts"; await Save(db, run);
                var baseDir = string.IsNullOrWhiteSpace(buildConfig.OutputPath)
                    ? (string.IsNullOrWhiteSpace(buildConfig.WorkingDirectory) ? repo.LocalPath! : Path.Combine(repo.LocalPath!, buildConfig.WorkingDirectory!))
                    : (Path.IsPathRooted(buildConfig.OutputPath!) ? buildConfig.OutputPath! : Path.Combine(repo.LocalPath!, buildConfig.OutputPath!));
                if (!string.IsNullOrWhiteSpace(buildConfig.WorkingDirectory) && !Path.IsPathRooted(baseDir))
                    baseDir = Path.Combine(repo.LocalPath!, buildConfig.WorkingDirectory!);
                var uploadRes = await _sftp.UploadAsync(sftpConfig, artifacts, baseDir, log, ct);
                if (!uploadRes.Success) throw new Exception("SFTP upload failed: " + uploadRes.Error);
            }

            // Post-run commands (comma separated), e.g. restart the app after new version
            if (!string.IsNullOrWhiteSpace(profile.PostRunCommands))
            {
                run.Status = "PostRunning"; run.CurrentStep = "Running post-deployment commands"; await Save(db, run);
                foreach (var raw in profile.PostRunCommands.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    log($"Post-run (remote): {raw}");
                    var res = await _sftp.RunRemoteCommandAsync(sftpConfig, raw, log, ct);
                    if (!res.Success) throw new Exception($"Post-run command failed: {raw} - {res.Error}");
                }
            }

            run.Status = "Completed"; run.CurrentStep = "Done"; run.Success = true; run.FinishedAt = DateTime.UtcNow;
            await Save(db, run);
            log("Release completed successfully.");
        }
        catch (Exception ex)
        {
            run.Status = "Failed"; run.CurrentStep = "Failed"; run.Success = false; run.ErrorMessage = ex.Message; run.FinishedAt = DateTime.UtcNow;
            await Save(db, run);
            log("ERROR: " + ex.Message);
        }
    }

    private static void Log(AppDbContext db, ReleaseRun run, string message)
    {
        var entry = new ReleaseRunLog { ReleaseRunId = run.Id, Message = message, Timestamp = DateTime.UtcNow };
        db.ReleaseRunLogs.Add(entry);
        db.SaveChanges();
    }

    private static Task Save(AppDbContext db, ReleaseRun run)
    {
        db.ReleaseRuns.Update(run);
        return db.SaveChangesAsync();
    }
}