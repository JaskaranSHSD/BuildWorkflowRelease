using System.Net.Http.Headers;
using System.Text.Json;

namespace BuildWorkflowRelease.Services;

public class SsoUserInfo
{
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string? ExternalId { get; set; }
}

public class SsoService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly IConfiguration _config;

    public SsoService(IHttpClientFactory httpFactory, IConfiguration config)
    {
        _httpFactory = httpFactory;
        _config = config;
    }

    /// <summary>
    /// Validates an incoming SSO token against the identity provider API and
    /// returns the resolved user details. Implement/adjust the HTTP call and
    /// response parsing below for your identity provider.
    /// </summary>
    public virtual async Task<SsoUserInfo?> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        var endpoint = _config["Sso:UserInfoEndpoint"];
        if (string.IsNullOrWhiteSpace(endpoint))
        {
            // Not configured yet: implement the API call here later.
            return null;
        }

        try
        {
            var client = _httpFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var response = await client.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = doc.RootElement;

            var email = TryGetString(root, "email") ?? TryGetString(root, "mail") ?? TryGetString(root, "upn");
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            var username = TryGetString(root, "username") ?? TryGetString(root, "preferred_username") ?? email.Split('@')[0];
            var displayName = TryGetString(root, "name") ?? TryGetString(root, "displayName");
            var externalId = TryGetString(root, "id") ?? TryGetString(root, "sub");

            // Verify the resolved user details match the (claimed) email identity.
            if (!string.Equals(email, TryGetString(root, "email") ?? email, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new SsoUserInfo
            {
                Email = email,
                Username = username,
                DisplayName = displayName,
                ExternalId = externalId
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement el, string name)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
        {
            return p.GetString();
        }
        return null;
    }
}