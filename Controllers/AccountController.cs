using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace BuildWorkflowRelease.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<User> _hasher = new();

    public AccountController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string usernameOrEmail, string password, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(usernameOrEmail) || string.IsNullOrWhiteSpace(password))
        {
            ModelState.AddModelError(string.Empty, "Username/email and password are required.");
            return View();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u =>
            u.Username == usernameOrEmail || u.Email == usernameOrEmail);

        if (user == null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View();
        }

        var result = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View();
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();
}