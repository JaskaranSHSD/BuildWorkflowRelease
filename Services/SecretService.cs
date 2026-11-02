using Microsoft.AspNetCore.DataProtection;

namespace BuildWorkflowRelease.Services;

public class SecretService
{
    private readonly IDataProtector _protector;

    public SecretService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("BuildWorkflowRelease.Secrets.v1");
    }

    public string? Protect(string? plain) => string.IsNullOrEmpty(plain) ? null : _protector.Protect(plain);
    public string? Unprotect(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return null;
        try { return _protector.Unprotect(cipher); }
        catch { return null; }
    }
}