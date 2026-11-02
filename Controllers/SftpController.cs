using BuildWorkflowRelease.Data;
using BuildWorkflowRelease.Models.Entities;
using BuildWorkflowRelease.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BuildWorkflowRelease.Controllers
{
    public class SftpController : Controller
    {
        private readonly AppDbContext _db;
        private readonly SecretService _secrets;
        public SftpController(AppDbContext db, SecretService secrets) { _db = db; _secrets = secrets; }

        public async Task<IActionResult> Index() => View(await _db.SftpConfigs.ToListAsync());

        public IActionResult Create() => View(new SftpConfig());

        [HttpPost]
        public async Task<IActionResult> Create(SftpConfig model, string? plainPassword, string? plainPassphrase)
        {
            model.PasswordEncrypted = _secrets.Protect(plainPassword);
            model.PrivateKeyPassphraseEncrypted = _secrets.Protect(plainPassphrase);
            _db.SftpConfigs.Add(model);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var s = await _db.SftpConfigs.FindAsync(id);
            if (s == null) return NotFound();
            return View(s);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(int id, SftpConfig model, string? plainPassword, string? plainPassphrase)
        {
            var s = await _db.SftpConfigs.FindAsync(id);
            if (s == null) return NotFound();
            s.Name = model.Name;
            s.Host = model.Host;
            s.Port = model.Port;
            s.Username = model.Username;
            s.PrivateKeyPath = model.PrivateKeyPath;
            s.RemotePath = model.RemotePath;
            if (!string.IsNullOrEmpty(plainPassword)) s.PasswordEncrypted = _secrets.Protect(plainPassword);
            if (!string.IsNullOrEmpty(plainPassphrase)) s.PrivateKeyPassphraseEncrypted = _secrets.Protect(plainPassphrase);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var s = await _db.SftpConfigs.FindAsync(id);
            if (s != null) { _db.SftpConfigs.Remove(s); await _db.SaveChangesAsync(); }
            return RedirectToAction(nameof(Index));
        }
    }
}