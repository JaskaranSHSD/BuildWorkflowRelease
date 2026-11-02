using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using BuildWorkflowRelease.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildWorkflowRelease.Controllers
{
    public class BuildRunViewModel
    {
        public int BuildConfigId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> Logs { get; set; } = new();
        public List<string> Artifacts { get; set; } = new();
    }

    public class BuildsController : Controller
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<int, BuildRunViewModel> LastRuns =
            new System.Collections.Concurrent.ConcurrentDictionary<int, BuildRunViewModel>();

        private readonly AppDbContext _db;
        private readonly GitService _git;
        private readonly BuildService _build;
        public BuildsController(AppDbContext db, GitService git, BuildService build) { _db = db; _git = git; _build = build; }

        public async Task<IActionResult> Index() => View(await _db.Builds.Include(b => b.Repository).ToListAsync());

        public async Task<IActionResult> Create()
        {
            ViewBag.Repositories = await _db.Repositories.ToListAsync();
            return View(new BuildConfig());
        }

        [HttpPost]
        public async Task<IActionResult> Create(BuildConfig model)
        {
            _db.Builds.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var b = await _db.Builds.FindAsync(id);
            if (b == null) return NotFound();
            ViewBag.Repositories = await _db.Repositories.ToListAsync();
            return View(b);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, BuildConfig model)
        {
            var b = await _db.Builds.FindAsync(id);
            if (b == null) return NotFound();
            b.Name = model.Name;
            b.RepositoryId = model.RepositoryId;
            b.BuildCommand = model.BuildCommand;
            b.WorkingDirectory = model.WorkingDirectory;
            b.OutputPath = model.OutputPath;
            b.ArtifactGlob = model.ArtifactGlob;
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var b = await _db.Builds.FindAsync(id);
            if (b != null) { _db.Builds.Remove(b); await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Run(int id)
        {
            var build = await _db.Builds.Include(b => b.Repository).FirstOrDefaultAsync(b => b.Id == id);
            if (build == null) return NotFound();

            var vm = new BuildRunViewModel { BuildConfigId = build.Id, Name = build.Name };
            var logs = new List<string>();
            Action<string> log = m => logs.Add(m);

            try
            {
                if (build.Repository == null) throw new InvalidOperationException("Repository not configured for this build.");
                // fetch latest
                var pullRes = await _git.CloneOrPullAsync(build.Repository, log, HttpContext.RequestAborted);
                if (!pullRes.Success) throw new Exception("Git fetch failed: " + pullRes.Output);

                var buildRes = await _build.RunAsync(build, log, HttpContext.RequestAborted);
                if (!buildRes.Success) throw new Exception("Build failed");
                vm.Artifacts = buildRes.Artifacts;
                vm.Success = true;
            }
            catch (Exception ex)
            {
                vm.Success = false;
                vm.ErrorMessage = ex.Message;
                log("ERROR: " + ex.Message);
            }
            vm.Logs = logs;
            LastRuns[build.Id] = vm;
            return View("Run", vm);
        }

        public IActionResult Logs(int id)
        {
            if (LastRuns.TryGetValue(id, out var vm)) return View(vm);
            var empty = new BuildRunViewModel { BuildConfigId = id, Name = "Build #" + id, Logs = new List<string> { "No run logs available yet. Run the build first." } };
            return View(empty);
        }
    }
}