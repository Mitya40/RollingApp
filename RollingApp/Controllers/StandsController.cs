using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using RollingApp.Data;
using RollingApp.Models.Domain;

namespace RollingApp.Controllers
{
    [Authorize]
    public class StandsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StandsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Страница клетей конкретного стана
        public async Task<IActionResult> Index(int id)
        {
            // Ищем стан по id
            var mill = await _context.Mills.FindAsync(id);
            if (mill == null) return NotFound();

            ViewBag.Mill = mill;

            // Ищем клети, у которых MillId равен нашему id
            var stands = await _context.Stands
                .Where(s => s.MillId == id)
                .OrderBy(s => s.SequenceNumber)
                .ToListAsync();

            return View(stands);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SaveStand([FromBody] Stand stand)
        {
            if (stand.Id == 0)
            {
                _context.Stands.Add(stand);
            }
            else
            {
                var existing = await _context.Stands.FindAsync(stand.Id);
                if (existing == null) return Json(new { success = false });

                // ВАЖНО: Строгое копирование параметров оборудования из C++
                existing.SequenceNumber = stand.SequenceNumber;
                existing.Name = stand.Name;
                existing.DB = stand.DB;
                existing.DSH = stand.DSH;
                existing.MU = stand.MU;
                existing.FPOD = stand.FPOD;
                existing.LKL = stand.LKL;
                existing.AKL = stand.AKL;
                existing.IR = stand.IR;
                existing.ETA = stand.ETA;
                existing.NZ = stand.NZ;
                existing.NNOM = stand.NNOM;
                existing.PP = stand.PP;
                existing.NDVN = stand.NDVN;
                existing.NDVMIN = stand.NDVMIN;
                existing.NDVMAX = stand.NDVMAX;
                existing.PDOP = stand.PDOP;
                existing.MDOP = stand.MDOP;
                existing.C = stand.C;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, id = stand.Id });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteStand([FromBody] int id)
        {
            var stand = await _context.Stands.FindAsync(id);
            if (stand == null) return Json(new { success = false });

            _context.Stands.Remove(stand);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}