using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using RollingApp.Data;
using RollingApp.Models.Calculation;
using RollingApp.Models.Domain;
using RollingApp.Services;

using System.Security.Claims;
using System.Text.Json;

namespace RollingApp.Controllers
{
    [Authorize]
    public class CalibrationController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly CalculationEngineService _calcEngine;
        private readonly ValidationService _validationService;
        private readonly EvaluateRestrictionsService _evaluateRestrictionsService;

        public CalibrationController(ApplicationDbContext context, CalculationEngineService calcEngine, ValidationService validationService, EvaluateRestrictionsService evaluateRestrictionsService)
        {
            _context = context;
            _calcEngine = calcEngine;
            _validationService = validationService;
            _evaluateRestrictionsService = evaluateRestrictionsService;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int id)
        {
            var profile = await _context.Profiles
                .Include(p => p.Mill)
                .Include(p => p.InitialParameter)
                .Include(p => p.Passes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profile == null) return NotFound();

            ViewBag.Steels = await _context.Steels.OrderBy(s => s.Name).ToListAsync();
            ViewBag.Stands = await _context.Stands.Where(s => s.MillId == profile.MillId).OrderBy(s => s.SequenceNumber).ToListAsync();

            return View(profile);
        }

        [HttpPost]
        public async Task<IActionResult> SaveCalibration([FromBody] CalibrationSaveDto data)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAdmin = User.IsInRole("Admin");

            var profile = await _context.Profiles
                .Include(p => p.InitialParameter)
                .Include(p => p.Passes)
                .FirstOrDefaultAsync(p => p.Id == data.ProfileId);

            if (profile == null) return Json(new { success = false, message = "Профиль не найден" });

            // Проверка прав: Оператор может редактировать только свои профили
            if (!isAdmin && profile.CreatorId != userId && profile.CreatorId != null)
                return Json(new { success = false, message = "У вас нет прав редактировать этот профиль." });

            Profile targetProfile = profile;

            if (data.IsCorrection)
            {
                targetProfile = new Profile
                {
                    Name = $"Корректировка: {profile.Name}",
                    MillId = profile.MillId,
                    ParentProfileId = profile.Id,
                    IsTemplate = false,
                    CreationDate = DateTime.Now,
                    SteelId = data.SteelId
                };
                _context.Profiles.Add(targetProfile);
            }
            else
            {
                targetProfile.SteelId = data.SteelId;
                targetProfile.IsTemplate = data.IsTemplate;
            }

            // Сохранение начальных параметров
            var initParams = targetProfile.InitialParameter ?? new InitialParameter();
            initParams.TargetDate = data.TargetDate;
            initParams.W0 = data.W0;
            initParams.P0 = data.P0;
            initParams.L0 = data.L0;
            initParams.T0 = data.T0;
            initParams.TAU = data.TAU;
            initParams.LR = data.LR;
            initParams.VK = data.VK;

            if (targetProfile.InitialParameter == null)
                targetProfile.InitialParameter = initParams;

            // Сохранение проходов
            if (!data.IsCorrection)
            {
                _context.Passes.RemoveRange(targetProfile.Passes);
            }

            // Подгружаем физические клети этого стана
            var millStands = await _context.Stands.Where(s => s.MillId == targetProfile.MillId).ToListAsync();

            foreach (var p in data.Passes)
            {
                // АВТОМАТИЧЕСКАЯ ПРИВЯЗКА: Клеть физически должна соответствовать номеру прохода N
                var stand = millStands.FirstOrDefault(s => s.SequenceNumber == p.N);

                if (stand == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Ошибка целостности: Для прохода N={p.N} не найдено оборудование. " +
                                  $"Администратор должен добавить клеть №{p.N} в разделе 'Оборудование (Станы)'."
                    });
                }

                targetProfile.Passes.Add(new Pass
                {
                    N = p.N,
                    KOD0 = p.KOD0,
                    KOD1 = p.KOD1,
                    H0 = p.H0,
                    B0 = p.B0,
                    H1 = p.H1,
                    B1 = p.B1,
                    W = p.W,
                    S = p.S,
                    BVR = p.BVR,
                    BD = p.BD,
                    R = p.R,
                    ROV = p.ROV,
                    R8 = p.R8,
                    SUMX = p.SUMX,
                    PSI = p.PSI,
                    Z = p.Z,
                    SP = p.SP,
                    TOP = p.TOP,
                    StandId = stand.Id // Присваиваем автоматически найденный ID клети
                });
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true, profileId = targetProfile.Id });
        }

        [HttpGet]
        public async Task<IActionResult> Config(int id)
        {
            var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.Id == id);
            if (profile == null) return NotFound();

            ViewBag.ProfileName = profile.Name;

            // Задаем дефолтные настройки
            var settings = new CalculationSettings
            {
                K10 = 1, // Лучеиспускание и разогрев
                K11 = 3, // Метод ОМД
                K12 = 0, // Расчетная частота вращения
                K13 = 7, // Расчетный температурный режим
                K14 = 9, // Расчетные площади
                K15 = 11, // Расчетные вытяжки
                K16 = 1, // Заполнение калибров
                K17 = 1, // Расчет формоизменения: да
                TS = 1   // Непрерывный стан
            };

            return View(settings);
        }

        [HttpPost]
        public IActionResult RunCalculation(int id, CalculationSettings settings)
        {
            // Сохраняем настройки в TempData
            TempData["CalcSettings"] = JsonSerializer.Serialize(settings);
            return RedirectToAction("Results", new { id = id });
        }

        [HttpGet]
        public async Task<IActionResult> Results(int id)
        {
            if (TempData["CalcSettings"] is not string settingsJson)
                return RedirectToAction("Config", new { id = id });

            var settings = JsonSerializer.Deserialize<CalculationSettings>(settingsJson);

            var profile = await _context.Profiles
                .Include(p => p.InitialParameter)
                .Include(p => p.Passes)
                .Include(p => p.Steel)
                .Include(p => p.Mill)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (profile == null || profile.InitialParameter == null || profile.Steel == null)
                return NotFound("Не найден профиль, начальные параметры или марка стали.");

            var stands = await _context.Stands.Where(s => s.MillId == profile.MillId).ToListAsync();

            var context = _calcEngine.RunCalculation(profile.InitialParameter, profile.Passes.ToList(), stands, profile.Steel, settings!);

            ViewBag.ProfileName = profile.Name;
            ViewBag.HasErrors = context.CalculationErrors.Any();

            ViewBag.SettingsSummary = new Dictionary<string, string> {
                { "Метод расчета", settings.CalculationMethod == 1 ? "Кафедры ОМД УГТУ" : "Соответственной полосы" },
                { "Сопротивление деформации", settings.K11 == 1 ? "Метод Зюзина В.И." : settings.K11 == 2 ? "Метод Андреюка и Тюленева" : "С учетом динам. разупрочнения" },
                { "Тепловой баланс", settings.K10 == 1 ? "Лучеиспускание и разогрев" : "Лучеиспускание, конвекция, теплопроводность и разогрев"},
                { "Тип стана", settings.TS == 1 ? "Непрерывный" : (settings.TS == 2 ? "Последовательный" : "С петлевой группой") },
                { "Расчет формоизменения", settings.K17 == 1 ? "Выполнялся" : "Нет" },
                { "Площади сечений", settings.K14 == 8 ? "Заданные (W)" : "Расчетные (omega)" },
                { "Коэффициенты вытяжки", settings.K15 == 10 ? "Заданные" : "Расчетные" },
                { "Температурный режим", settings.K13 == 9 ? "Опытный (по замерам)" : "Расчетный (энергобаланс)" },
                { "Частота вращения валков", settings.K12 == 1 ? "Заданная (фактическая)" : "Расчетная (кинематика)" }
            };
            ViewBag.Settings = settings;

            var expertReports = _evaluateRestrictionsService.EvaluateCalculationResults(context);
            ViewBag.RestrictionsReports = expertReports;

            return View(context);
        }

        [HttpPost]
        public async Task<IActionResult> MakeCopy(int id)
        {
            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // 1. Загружаем оригинал со всеми зависимостями
            var original = await _context.Profiles
                .Include(p => p.InitialParameter)
                .Include(p => p.Passes)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (original == null)
                return Json(new { success = false, message = "Оригинал не найден" });

            // 2. Создаем новый объект профиля
            var copy = new Profile
            {
                Name = $"[Копия] {original.Name}",
                MillId = original.MillId,
                SteelId = original.SteelId,
                IsTemplate = false,
                CreationDate = DateTime.Now,
                CreatorId = currentUserId ?? "",
                ParentProfileId = original.Id // Cвязь с родителем
            };

            // 3. Копируем начальные параметры (если есть)
            if (original.InitialParameter != null)
            {
                copy.InitialParameter = new InitialParameter
                {
                    TargetDate = DateTime.Now,
                    W0 = original.InitialParameter.W0,
                    P0 = original.InitialParameter.P0,
                    L0 = original.InitialParameter.L0,
                    T0 = original.InitialParameter.T0,
                    TAU = original.InitialParameter.TAU,
                    LR = original.InitialParameter.LR,
                    VK = original.InitialParameter.VK,
                    CreatorId = currentUserId ?? ""
                };
            }

            // 4. Копируем проходы
            foreach (var p in original.Passes.OrderBy(x => x.N))
            {
                copy.Passes.Add(new Pass
                {
                    N = p.N,
                    KOD0 = p.KOD0,
                    KOD1 = p.KOD1,
                    H0 = p.H0,
                    B0 = p.B0,
                    H1 = p.H1,
                    B1 = p.B1,
                    W = p.W,
                    S = p.S,
                    BVR = p.BVR,
                    BD = p.BD,
                    R = p.R,
                    ROV = p.ROV,
                    R8 = p.R8,
                    SUMX = p.SUMX,
                    PSI = p.PSI,
                    Z = p.Z,
                    SP = p.SP,
                    TOP = p.TOP,
                    StandId = p.StandId,
                    CreatorId = currentUserId ?? ""
                });
            }

            _context.Profiles.Add(copy);
            await _context.SaveChangesAsync();

            return Json(new { success = true, newId = copy.Id });
        }

        [HttpPost]
        public IActionResult Diagnose([FromBody] CalibrationSaveDto data)
        {
            if (data == null) return Json(new { success = false, message = "Нет данных" });

            // Преобразуем DTO во временные модели
            var tempInit = new InitialParameter { W0 = data.W0, T0 = data.T0 };
            var tempPasses = data.Passes.Select(p => new Pass
            {
                N = p.N,
                KOD0 = p.KOD0,
                KOD1 = p.KOD1,
                H0 = p.H0,
                B0 = p.B0,
                H1 = p.H1,
                B1 = p.B1,
                S = p.S,
                BVR = p.BVR,
                BD = p.BD,
                R = p.R,
                ROV = p.ROV
            }).ToList();

            var validationService = new ValidationService();
            var result = validationService.Diagnose(tempInit, tempPasses);

            return Json(new { success = true, hasErrors = result.HasCriticalErrors, messages = result.Messages });
        }
    }

    // DTO для получения JSON с фронта
    public class CalibrationSaveDto
    {
        public int ProfileId { get; set; }
        public bool IsCorrection { get; set; }
        public bool IsTemplate { get; set; }
        public int? SteelId { get; set; }
        public DateTime TargetDate { get; set; }
        public double W0 { get; set; }
        public double P0 { get; set; }
        public double L0 { get; set; }
        public double T0 { get; set; }
        public double TAU { get; set; }
        public double LR { get; set; }
        public double VK { get; set; }
        public List<PassDto> Passes { get; set; } = new();
    }

    public class PassDto
    {
        public int N { get; set; }
        public int KOD0 { get; set; }
        public int KOD1 { get; set; }
        public double H0 { get; set; }
        public double B0 { get; set; }
        public double H1 { get; set; }
        public double B1 { get; set; }
        public double W { get; set; }
        public double S { get; set; }
        public double BVR { get; set; }
        public double BD { get; set; }
        public double R { get; set; }
        public double ROV { get; set; }
        public double R8 { get; set; }
        public double SUMX { get; set; }
        public double PSI { get; set; }
        public int Z { get; set; }
        public int SP { get; set; }
        public double TOP { get; set; }
    }
}