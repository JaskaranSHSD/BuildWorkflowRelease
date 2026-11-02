using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using BuildWorkflowRelease.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildWorkflowRelease.Controllers
{
    public class RepositoriesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly SecretService _secrets;
        public RepositoriesController(AppDbContext db, SecretService secrets) { _db = db; _secrets = secrets; }

        public async Task<IActionResult> Index() => View(await _db.Repositories.ToListAsync());

        public IActionResult Create() => View(new RepositoryConfig());

        [HttpPost]
        public async Task<IActionResult> Create(RepositoryConfig model, string? plainToken)
        {
            if (string.IsNullOrWhiteSpace(model.LocalPath))
            {
                var safeName = string.Concat((model.Name ?? "repo").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim('_');
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "repo";
                model.LocalPath = Path.Combine(Path.GetTempPath(), "BuildWorkflowRelease", "repos", $"{safeName}-{Guid.NewGuid():N}");
            }
            model.LocalPath = Path.GetFullPath(model.LocalPath);
            model.AccessTokenEncrypted = _secrets.Protect(plainToken);
            _db.Repositories.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var r = await _db.Repositories.FindAsync(id);
            if (r == null) return NotFound();
            return View(r);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, RepositoryConfig model, string? plainToken)
        {
            var r = await _db.Repositories.FindAsync(id);
            if (r == null) return NotFound();
            r.Name = model.Name;
            r.GitUrl = model.GitUrl;
            r.Branch = model.Branch;
            if (string.IsNullOrWhiteSpace(model.LocalPath))
            {
                var safeName = string.Concat((model.Name ?? "repo").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)).Trim('_');
                if (string.IsNullOrWhiteSpace(safeName)) safeName = "repo";
                r.LocalPath = Path.Combine(Path.GetTempPath(), "BuildWorkflowRelease", "repos", $"{safeName}-{Guid.NewGuid():N}");
            }
            else
            {
                r.LocalPath = Path.GetFullPath(model.LocalPath);
            }
            r.Username = model.Username;
            if (!string.IsNullOrEmpty(plainToken)) r.AccessTokenEncrypted = _secrets.Protect(plainToken);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var r = await _db.Repositories.FindAsync(id);
            if (r != null) { _db.Repositories.Remove(r); await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CleanData(int id)
        {
            var r = await _db.Repositories.FindAsync(id);
            if (r != null && !string.IsNullOrWhiteSpace(r.LocalPath))
            {
                var localPath = Path.GetFullPath(r.LocalPath);
                var tempRoot = Path.GetFullPath(Path.GetTempPath());
                if (localPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(localPath))
                {
                    try { Directory.Delete(localPath, true); }
                    catch { }
                }
            }
            TempData["Status"] = "Repository data cleaned.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> CleanAllData()
        {
            var repos = await _db.Repositories.ToListAsync();
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            foreach (var r in repos)
            {
                if (string.IsNullOrWhiteSpace(r.LocalPath)) continue;
                var localPath = Path.GetFullPath(r.LocalPath);
                if (localPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(localPath))
                {
                    try { Directory.Delete(localPath, true); } catch { }
                }
            }
            TempData["Status"] = "All repository temp data cleaned.";
            return RedirectToAction(nameof(Index));
        }
    }
}