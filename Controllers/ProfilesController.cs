using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using BuildWorkflowRelease.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildWorkflowRelease.Controllers
{
    public class ProfilesController : Controller
    {
        private readonly AppDbContext _db;
        private readonly ReleaseOrchestrator _orch;
        public ProfilesController(AppDbContext db, ReleaseOrchestrator orch) { _db = db; _orch = orch; }

        public async Task<IActionResult> Index() =>
            View(await _db.ReleaseProfiles.Include(p => p.BuildConfig).Include(p => p.SftpConfig).ToListAsync());

        public async Task<IActionResult> Create()
        {
            ViewBag.Builds = await _db.Builds.ToListAsync();
            ViewBag.Sftp = await _db.SftpConfigs.ToListAsync();
            return View(new ReleaseProfile());
        }

        [HttpPost]
        public async Task<IActionResult> Create(ReleaseProfile model)
        {
            _db.ReleaseProfiles.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var p = await _db.ReleaseProfiles.FindAsync(id);
            if (p == null) return NotFound();
            ViewBag.Builds = await _db.Builds.ToListAsync();
            ViewBag.Sftp = await _db.SftpConfigs.ToListAsync();
            return View(p);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, ReleaseProfile model)
        {
            var p = await _db.ReleaseProfiles.FindAsync(id);
            if (p == null) return NotFound();
            p.Name = model.Name;
            p.BuildConfigId = model.BuildConfigId;
            p.SftpConfigId = model.SftpConfigId;
            p.AutoPull = model.AutoPull;
            p.AutoBuild = model.AutoBuild;
            p.AutoUpload = model.AutoUpload;
            p.PostRunCommands = model.PostRunCommands;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var p = await _db.ReleaseProfiles.FindAsync(id);
            if (p != null) { _db.ReleaseProfiles.Remove(p); await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> StartRelease(int id)
        {
            var profile = await _db.ReleaseProfiles.FindAsync(id);
            if (profile == null) return NotFound();
            var run = new ReleaseRun { ReleaseProfileId = id, Status = "Pending" };
            _db.ReleaseRuns.Add(run);
            await _db.SaveChangesAsync();
            _orch.Start(id, run.Id);
            return RedirectToAction("Details", "Releases", new { id = run.Id });
        }
    }
}