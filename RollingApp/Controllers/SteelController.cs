using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using RollingApp.Data;
using RollingApp.Models.Domain;

using System.Security.Claims;

namespace RollingApp.Controllers
{
    [Authorize]
    public class SteelsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SteelsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var steels = await _context.Steels.OrderBy(s => s.Name).ToListAsync();
            return View(steels);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> SaveSteel([FromBody] Steel data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Name))
                return Json(new { success = false, message = "Некорректные данные" });

            if (data.Id == 0) // Создание новой марки
            {
                data.CreatorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
                _context.Steels.Add(data);
            }
            else // Обновление существующей
            {
                var existing = await _context.Steels.FindAsync(data.Id);
                if (existing == null) return Json(new { success = false, message = "Сталь не найдена" });

                existing.Name = data.Name;
                existing.M = data.M;
                existing.K1 = data.K1; existing.K2 = data.K2; existing.K3 = data.K3;
                existing.K4 = data.K4; existing.K5 = data.K5; existing.K6 = data.K6;
                existing.K7 = data.K7; existing.K8 = data.K8; existing.K9 = data.K9;

                _context.Steels.Update(existing);
            }

            await _context.SaveChangesAsync();

            // ВАЖНО: Возвращаем сгенерированный Id обратно на клиент
            return Json(new { success = true, id = data.Id });
        }

        [HttpPost]
        [Authorize(Roles = "Admin")] // Только Админ может удалять
        public async Task<IActionResult> DeleteSteel([FromBody] int id)
        {
            var steel = await _context.Steels.FindAsync(id);
            if (steel == null) return Json(new { success = false, message = "Марка стали не найдена." });

            // ПРОАКТИВНАЯ ПРОВЕРКА: Используется ли сталь в профилях?
            bool isUsedInProfiles = await _context.Profiles.AnyAsync(p => p.SteelId == id);
            if (isUsedInProfiles)
            {
                return Json(new
                {
                    success = false,
                    message = "Невозможно удалить марку стали, так как она уже используется в сохраненных маршрутах прокатки (профилях). Сначала измените сталь в профилях или удалите сами профили."
                });
            }

            try
            {
                _context.Steels.Remove(steel);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Системная ошибка БД при удалении." });
            }
        }
    }
}