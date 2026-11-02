using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using BuildWorkflowRelease.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildWorkflowRelease.Controllers;

[AllowAnonymous]
public class SSOController : Controller
{
    private readonly AppDbContext _db;
    private readonly SsoService _sso;
    private readonly IConfiguration _config;
    private readonly PasswordHasher<User> _hasher = new();

    public SSOController(AppDbContext db, SsoService sso, IConfiguration config)
    {
        _db = db;
        _sso = sso;
        _config = config;
    }

    // Receives the SSO token (query string or form body), validates it against
    // the identity provider API, matches/creates the local user and signs in.
    [IgnoreAntiforgeryToken]
    [AcceptVerbs("GET", "POST")]
    public async Task<IActionResult> Callback(string? token, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            token = Request.Query["token"].ToString();
        }
        if (string.IsNullOrWhiteSpace(token))
        {
            token = Request.Form["token"].ToString();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            TempData["Error"] = "Missing SSO token.";
            return RedirectToAction("Login", "Account");
        }

        var info = await _sso.ValidateTokenAsync(token);
        if (info == null || string.IsNullOrWhiteSpace(info.Email))
        {
            TempData["Error"] = "SSO authentication failed.";
            return RedirectToAction("Login", "Account");
        }

        // Match on email; create the user if it does not already exist.
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == info.Email);

        if (user == null)
        {
            var username = string.IsNullOrWhiteSpace(info.Username)
                ? info.Email.Split('@')[0]
                : info.Username;

            // Ensure username uniqueness.
            var baseUsername = username;
            var i = 1;
            while (await _db.Users.AnyAsync(u => u.Username == username))
            {
                username = $"{baseUsername}{i++}";
            }

            user = new User
            {
                Username = username,
                Email = info.Email,
                DisplayName = info.DisplayName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            // SSO-only account: random unusable password.
            user.PasswordHash = _hasher.HashPassword(user, Guid.NewGuid().ToString());
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }
        else
        {
            // Check user details match the email identity; update non-conflicting fields.
            if (!user.IsActive)
            {
                TempData["Error"] = "User account is disabled.";
                return RedirectToAction("Login", "Account");
            }

            if (!string.IsNullOrWhiteSpace(info.DisplayName) && user.DisplayName != info.DisplayName)
            {
                user.DisplayName = info.DisplayName;
            }
            if (!string.IsNullOrWhiteSpace(info.Username) &&
                !string.Equals(user.Username, info.Username, StringComparison.OrdinalIgnoreCase))
            {
                // Keep existing username unless free to avoid collisions.
                if (!await _db.Users.AnyAsync(u => u.Username == info.Username && u.Id != user.Id))
                {
                    user.Username = info.Username;
                }
            }
            user.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email)
        };
        if (!string.IsNullOrEmpty(user.DisplayName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.DisplayName));
        }
        claims.Add(new Claim("sso", "true"));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", "Home");
    }

    // Convenience entry point that redirects to the identity provider (implement URL later).
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var ssoUrl = _config["Sso:LoginUrl"];
        if (!string.IsNullOrWhiteSpace(ssoUrl))
        {
            var redirect = Url.Action("Callback", "SSO", new { returnUrl }, Request.Scheme) ?? "/";
            return Redirect($"{ssoUrl}?redirect={Uri.EscapeDataString(redirect)}");
        }

        // Fallback: accept token directly via form for testing.
        ViewBag.ReturnUrl = returnUrl;
        return View("TokenEntry");
    }

}