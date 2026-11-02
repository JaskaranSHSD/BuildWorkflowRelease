using Renci.SshNet;
using BuildWorkflowRelease.Models.Entities;

namespace BuildWorkflowRelease.Services;

public class SftpService
{
    private readonly SecretService _secrets;

    public SftpService(SecretService secrets) => _secrets = secrets;

    public Task<SftpResult> UploadAsync(SftpConfig cfg, IEnumerable<string> localFiles, string baseDir, Action<string> log, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var result = new SftpResult();
            try
            {
                using var client = CreateClient(cfg);
                client.Connect();
                log($"Connected to {cfg.Host}:{cfg.Port}");

                var remoteBase = string.IsNullOrWhiteSpace(cfg.RemotePath) ? "/" : cfg.RemotePath;
                EnsureRemoteDirectory(client, remoteBase);

                foreach (var file in localFiles)
                {
                    ct.ThrowIfCancellationRequested();
                    var relative = Path.GetRelativePath(baseDir, file).Replace('\\', '/');
                    var remoteFile = (remoteBase.TrimEnd('/') + "/" + relative).Replace("\\", "/");
                    var remoteDir = Path.GetDirectoryName(remoteFile)?.Replace('\\', '/');
                    if (!string.IsNullOrEmpty(remoteDir)) EnsureRemoteDirectory(client, remoteDir);
                    using var fs = File.OpenRead(file);
                    client.UploadFile(fs, remoteFile);
                    log($"Uploaded {relative} -> {remoteFile}");
                }
                client.Disconnect();
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = ex.Message;
                log($"SFTP error: {ex.Message}");
            }
            return result;
        }, ct);
    }

    public Task<(bool Success, string Error)> RunRemoteCommandAsync(SftpConfig cfg, string command, Action<string> log, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            try
            {
                using var client = CreateSshClient(cfg);
                client.Connect();
                log($"[remote] Connected to {cfg.Host}:{cfg.Port}");
                var remotePath = string.IsNullOrWhiteSpace(cfg.RemotePath) ? "." : cfg.RemotePath;
                var fullCmd = $"cd {remotePath} 2>/dev/null; {command}";
                using var cmd = client.CreateCommand(fullCmd);
                var asyncResult = cmd.BeginExecute();
                // stream output
                var reader = new StreamReader(cmd.OutputStream);
                var errReader = new StreamReader(cmd.ExtendedOutputStream);
                while (!asyncResult.IsCompleted)
                {
                    ct.ThrowIfCancellationRequested();
                    string? line;
                    while ((line = reader.ReadLine()) != null) log(line);
                    string? eline;
                    while ((eline = errReader.ReadLine()) != null) log(eline);
                    Thread.Sleep(100);
                }
                cmd.EndExecute(asyncResult);
                string? ol;
                while ((ol = reader.ReadLine()) != null) log(ol);
                string? el;
                while ((el = errReader.ReadLine()) != null) log(el);
                client.Disconnect();
                if (cmd.ExitStatus != 0)
                {
                    var err = string.IsNullOrWhiteSpace(cmd.Error) ? $"exit {cmd.ExitStatus}" : cmd.Error;
                    return (false, err);
                }
                return (true, string.Empty);
            }
            catch (Exception ex)
            {
                log($"SSH error: {ex.Message}");
                return (false, ex.Message);
            }
        }, ct);
    }

    private SshClient CreateSshClient(SftpConfig cfg)
    {
        if (!string.IsNullOrEmpty(cfg.PrivateKeyPath) && File.Exists(cfg.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(cfg.PrivateKeyPassphraseEncrypted)
                ? new PrivateKeyFile(cfg.PrivateKeyPath)
                : new PrivateKeyFile(cfg.PrivateKeyPath, _secrets.Unprotect(cfg.PrivateKeyPassphraseEncrypted));
            var auth = new PrivateKeyAuthenticationMethod(cfg.Username, keyFile);
            var connInfo = new Renci.SshNet.ConnectionInfo(cfg.Host, cfg.Port, cfg.Username, auth);
            return new SshClient(connInfo);
        }
        var password = _secrets.Unprotect(cfg.PasswordEncrypted) ?? string.Empty;
        return new SshClient(cfg.Host, cfg.Port, cfg.Username, password);
    }

    private SftpClient CreateClient(SftpConfig cfg)
    {
        if (!string.IsNullOrEmpty(cfg.PrivateKeyPath) && File.Exists(cfg.PrivateKeyPath))
        {
            var keyFile = string.IsNullOrEmpty(cfg.PrivateKeyPassphraseEncrypted)
                ? new PrivateKeyFile(cfg.PrivateKeyPath)
                : new PrivateKeyFile(cfg.PrivateKeyPath, _secrets.Unprotect(cfg.PrivateKeyPassphraseEncrypted));
            var auth = new PrivateKeyAuthenticationMethod(cfg.Username, keyFile);
            var connInfo = new Renci.SshNet.ConnectionInfo(cfg.Host, cfg.Port, cfg.Username, auth);
            return new SftpClient(connInfo);
        }
        var password = _secrets.Unprotect(cfg.PasswordEncrypted) ?? string.Empty;
        return new SftpClient(cfg.Host, cfg.Port, cfg.Username, password);
    }

    private static void EnsureRemoteDirectory(SftpClient client, string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = path.StartsWith("/") ? "/" : "";
        foreach (var p in parts)
        {
            current = current.TrimEnd('/') + "/" + p;
            if (!client.Exists(current))
            {
                client.CreateDirectory(current);
            }
        }
    }
}

public class SftpResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}