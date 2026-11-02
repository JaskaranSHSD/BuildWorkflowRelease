using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

// Authentication / authorization
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
            ? CookieSecurePolicy.SameAsRequest
            : CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddHttpClient();
builder.Services.AddSingleton<SsoService>();

// PostgreSQL
var conn = builder.Configuration.GetConnectionString("Default") ?? "Host=localhost;Port=5432;Database=buildworkflow;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(conn));

// Data protection + secrets
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SecretService>();

// Workflow services
builder.Services.AddSingleton<GitService>();
builder.Services.AddSingleton<BuildService>();
builder.Services.AddSingleton<SftpService>();
builder.Services.AddSingleton<ReleaseOrchestrator>();
builder.Services.AddSingleton<HealthCheckService>();
builder.Services.AddHostedService<HealthCheckBackgroundService>();

var app = builder.Build();

// Ensure DB schema (best-effort; configure ConnectionStrings:Default for your Postgres)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        // Seed default user (development only)
        if (app.Environment.IsDevelopment() && !db.Users.Any())
        {
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<BuildWorkflowRelease.Models.Entities.User>();
            var seed = new BuildWorkflowRelease.Models.Entities.User
            {
                Username = "admin",
                Email = "admin@example.com",
                DisplayName = "Administrator",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            seed.PasswordHash = hasher.HashPassword(seed, "Admin@123");
            db.Users.Add(seed);
            db.SaveChanges();
        }

    }
    catch (Exception ex)
    {
        Console.WriteLine("Database initialization failed: " + ex.Message);
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

// Basic security headers.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";
    await next();
});
app.MapStaticAssets().AllowAnonymous();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.Run();