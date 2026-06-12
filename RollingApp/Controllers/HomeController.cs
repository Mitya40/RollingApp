using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using RollingApp.Data;
using RollingApp.Models.Domain;

using System.Security.Claims;

namespace RollingApp.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var mills = await _context.Mills.OrderBy(m => m.Name).ToListAsync();
            return View(mills);
        }

        [HttpGet]
        public async Task<IActionResult> GetProfiles(int millId)
        {
            var profiles = await _context.Profiles
                .Where(p => p.MillId == millId)
                .OrderByDescending(p => p.CreationDate)
                .Select(p => new {
                    id = p.Id,
                    name = p.Name,
                    isTemplate = p.IsTemplate,
                    date = p.CreationDate.ToString("dd.MM.yyyy HH:mm")
                })
                .ToListAsync();

            return Json(profiles);
        }

        [HttpGet]
        public async Task<IActionResult> GetStands(int millId)
        {
            var stands = await _context.Stands
                .Where(s => s.MillId == millId)
                .OrderBy(s => s.SequenceNumber)
                .ToListAsync();

            return Json(stands);
        }

        [HttpPost]
        public async Task<IActionResult> CreateProfile(int millId, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return Json(new { success = false, message = "Имя пустое" });

            var profile = new Profile
            {
                MillId = millId,
                Name = name,
                CreationDate = DateTime.Now,
                IsTemplate = false
            };

            _context.Profiles.Add(profile);
            await _context.SaveChangesAsync();

            return Json(new { success = true, profileId = profile.Id });
        }
    }
}