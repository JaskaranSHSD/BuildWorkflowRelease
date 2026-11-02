using BuildWorkflowRelease.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildWorkflowRelease.Controllers
{
    public class ReleasesController : Controller
    {
        private readonly AppDbContext _db;
        public ReleasesController(AppDbContext db) => _db = db;

        public async Task<IActionResult> Index()
        {
            var runs = await _db.ReleaseRuns
                .Include(r => r.ReleaseProfile)
                .OrderByDescending(r => r.StartedAt)
                .ToListAsync();
            return View(runs);
        }

        public async Task<IActionResult> Details(int id)
        {
            var run = await _db.ReleaseRuns
                .Include(r => r.ReleaseProfile)
                .Include(r => r.Logs)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (run == null) return NotFound();
            return View(run);
        }
    }
}