using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using RollingApp.Data;
using RollingApp.Models.Domain;

namespace RollingApp.Controllers
{
    [Authorize]
    public class MillsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MillsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Страница списка станов
        public async Task<IActionResult> Index()
        {
            var mills = await _context.Mills
                .Include(m => m.Stands)
                .OrderBy(m => m.Id)
                .ToListAsync();
            return View(mills);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SaveMill([FromBody] Mill mill)
        {
            if (string.IsNullOrWhiteSpace(mill.Name))
                return Json(new { success = false, message = "Название стана не может быть пустым." });

            if (mill.Id == 0)
            {
                _context.Mills.Add(mill);
            }
            else
            {
                var existing = await _context.Mills.FindAsync(mill.Id);
                if (existing == null) return Json(new { success = false, message = "Стан не найден." });
                existing.Name = mill.Name;
                existing.Description = mill.Description;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, id = mill.Id });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteMill([FromBody] int id)
        {
            var mill = await _context.Mills.Include(m => m.Stands).FirstOrDefaultAsync(m => m.Id == id);
            if (mill == null) return Json(new { success = false });

            if (mill.Stands.Any())
                return Json(new { success = false, message = "Сначала удалите все клети этого стана." });

            _context.Mills.Remove(mill);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}