using System.Diagnostics;
using BuildWorkflowRelease.Models.Entities;

namespace BuildWorkflowRelease.Services;

public class GitResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
}

public class GitService
{
    private readonly SecretService _secrets;

    public GitService(SecretService secrets) => _secrets = secrets;

    public string GetAuthenticatedUrl(RepositoryConfig repo)
    {
        var url = repo.GitUrl;
        var token = _secrets.Unprotect(repo.AccessTokenEncrypted);
        if (!string.IsNullOrEmpty(token) && url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var user = string.IsNullOrEmpty(repo.Username) ? "x-access-token" : repo.Username;
            // inject credentials
            url = url.Replace("https://", $"https://{Uri.EscapeDataString(user)}:{Uri.EscapeDataString(token)}@");
        }
        return url;
    }

    public async Task<GitResult> CloneOrPullAsync(RepositoryConfig repo, Action<string> log, CancellationToken ct)
    {
        var url = GetAuthenticatedUrl(repo);
        var local = repo.LocalPath;
        if (string.IsNullOrWhiteSpace(local))
            throw new InvalidOperationException("LocalPath is required");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(local)) ?? ".");

        if (Directory.Exists(Path.Combine(local, ".git")))
        {
            log($"Repository exists at {local}; fetching...");
            var fetch = await RunGitAsync($"-C \"{local}\" fetch --all --tags", ct, log);
            if (!fetch.Success) return fetch;
            var checkout = await RunGitAsync($"-C \"{local}\" checkout {repo.Branch}", ct, log);
            if (!checkout.Success) return checkout;
            var pull = await RunGitAsync($"-C \"{local}\" pull origin {repo.Branch}", ct, log);
            return pull;
        }
        else
        {
            if (Directory.Exists(local))
            {
                // ensure empty-ish
            }
            else
            {
                Directory.CreateDirectory(local);
            }
            log($"Cloning {repo.GitUrl} into {local}...");
            var clone = await RunGitAsync($"clone --branch {repo.Branch} \"{url}\" \"{local}\"", ct, log);
            // remove credentials from remote url after clone for safety
            if (clone.Success)
            {
                await RunGitAsync($"-C \"{local}\" remote set-url origin {repo.GitUrl}", ct, _ => { });
            }
            return clone;
        }
    }

    private static async Task<GitResult> RunGitAsync(string args, CancellationToken ct, Action<string> log)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        psi.Environment["GIT_TERMINAL_PROMPT"] = "0";

        using var proc = new Process { StartInfo = psi };
        proc.Start();
        var outputTask = proc.StandardOutput.ReadToEndAsync(ct);
        var errorTask = proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        var output = await outputTask;
        var error = await errorTask;
        var combined = output + (string.IsNullOrEmpty(error) ? "" : "\n" + error);
        if (!string.IsNullOrWhiteSpace(combined)) log(combined.Trim());
        return new GitResult { Success = proc.ExitCode == 0, Output = combined };
    }
}