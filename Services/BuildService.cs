using System.Diagnostics;
using BuildWorkflowRelease.Models.Entities;

namespace BuildWorkflowRelease.Services;

public class BuildResult
{
    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;
    public List<string> Artifacts { get; set; } = new();
}

public class BuildService
{
    public async Task<BuildResult> RunAsync(BuildConfig build, Action<string> log, CancellationToken ct)
    {
        var workDir = string.IsNullOrWhiteSpace(build.WorkingDirectory)
            ? (string.IsNullOrWhiteSpace(build.Repository?.LocalPath) ? "." : build.Repository!.LocalPath!)
            : build.WorkingDirectory!;
        if (!Path.IsPathRooted(workDir) && !string.IsNullOrWhiteSpace(build.Repository?.LocalPath))
        {
            workDir = Path.Combine(build.Repository!.LocalPath!, workDir);
        }

        log($"Working directory: {workDir}");
        var psi = new ProcessStartInfo
        {
            FileName = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/sh",
            Arguments = OperatingSystem.IsWindows() ? $"/c {build.BuildCommand}" : $"-c \"{build.BuildCommand}\"",
            WorkingDirectory = workDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = new Process { StartInfo = psi };
        var output = new System.Text.StringBuilder();
        proc.OutputDataReceived += (_, e) => { if (e.Data != null) { output.AppendLine(e.Data); log(e.Data); } };
        proc.ErrorDataReceived += (_, e) => { if (e.Data != null) { output.AppendLine(e.Data); log(e.Data); } };
        proc.Start();
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        await proc.WaitForExitAsync(ct);

        var result = new BuildResult { Success = proc.ExitCode == 0, Output = output.ToString() };
        if (result.Success)
        {
            // collect artifacts
            var baseDir = string.IsNullOrWhiteSpace(build.OutputPath) ? workDir : build.OutputPath;
            if (!Path.IsPathRooted(baseDir)) baseDir = Path.Combine(workDir, baseDir);
            var pattern = string.IsNullOrWhiteSpace(build.ArtifactGlob) ? "*.*" : build.ArtifactGlob!;
            result.Artifacts = CollectFiles(baseDir, pattern).ToList();
            log($"Collected {result.Artifacts.Count} artifact(s).");
        }
        return result;
    }

    private static IEnumerable<string> CollectFiles(string baseDir, string pattern)
    {
        if (!Directory.Exists(baseDir)) yield break;
        // support glob-ish ** by simple recursion
        foreach (var file in Directory.EnumerateFiles(baseDir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(baseDir, file).Replace('\\', '/');
            if (MatchesGlob(rel, pattern)) yield return file;
        }
    }

    private static bool MatchesGlob(string path, string pattern)
    {
        // very simple: if pattern is **/* match all; otherwise ends-with/contains
        if (pattern is "**/*" or "*.*" or "*" or "") return true;
        // handle extension patterns like *.zip
        if (pattern.StartsWith("*."))
        {
            return path.EndsWith(pattern.Substring(1), StringComparison.OrdinalIgnoreCase);
        }
        return path.Contains(pattern, StringComparison.OrdinalIgnoreCase);
    }
}