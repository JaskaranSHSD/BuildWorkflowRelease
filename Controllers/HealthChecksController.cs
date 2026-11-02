using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using BuildWorkflowRelease.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildWorkflowRelease.Controllers
{
    public class HealthChecksController : Controller
    {
        private readonly AppDbContext _db;
        private readonly HealthCheckService _health;
        public HealthChecksController(AppDbContext db, HealthCheckService health) { _db = db; _health = health; }

        public async Task<IActionResult> Index() =>
            View(await _db.ApiHealthChecks.Include(c => c.SftpConfig).OrderByDescending(c => c.Id).ToListAsync());

        public async Task<IActionResult> Create()
        {
            ViewBag.Sftp = await _db.SftpConfigs.ToListAsync();
            return View(new ApiHealthCheck());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ApiHealthCheck model)
        {
            _db.ApiHealthChecks.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var c = await _db.ApiHealthChecks.FindAsync(id);
            if (c == null) return NotFound();
            ViewBag.Sftp = await _db.SftpConfigs.ToListAsync();
            return View(c);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, ApiHealthCheck model)
        {
            var c = await _db.ApiHealthChecks.FindAsync(id);
            if (c == null) return NotFound();
            c.Name = model.Name;
            c.Url = model.Url;
            c.ExpectedResponse = model.ExpectedResponse;
            c.Enabled = model.Enabled;
            c.IntervalMinutes = model.IntervalMinutes;
            c.RestartUrl = model.RestartUrl;
            c.NotifyUrl = model.NotifyUrl;
            c.SftpConfigId = model.SftpConfigId;
            c.FailureCommands = model.FailureCommands;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var c = await _db.ApiHealthChecks.FindAsync(id);
            if (c != null) { _db.ApiHealthChecks.Remove(c); await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Run(int id)
        {
            var check = await _db.ApiHealthChecks.Include(c => c.SftpConfig).FirstOrDefaultAsync(c => c.Id == id);
            if (check == null) return NotFound();
            var (healthy, message) = await _health.RunAsync(check, HttpContext.RequestAborted);
            TempData["LastResult"] = $"{check.Name}: {(healthy ? "HEALTHY" : "UNHEALTHY")} - {message}";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Logs(int id)
        {
            var check = await _db.ApiHealthChecks.FindAsync(id);
            if (check == null) return NotFound();
            var logs = await _db.HealthCheckLogs
                .Where(l => l.ApiHealthCheckId == id)
                .OrderByDescending(l => l.CheckedAt)
                .Take(100)
                .ToListAsync();
            ViewBag.Check = check;
            return View(logs);
        }
    }
}