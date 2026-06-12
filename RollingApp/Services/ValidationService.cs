using RollingApp.Models.Domain;

using System;
using System.Collections.Generic;
using System.Linq;

namespace RollingApp.Services
{
    public class ValidationResult
    {
        public bool HasCriticalErrors { get; set; }
        public List<string> Messages { get; set; } = new();
    }

    public class ValidationService
    {
        public ValidationResult Diagnose(InitialParameter init, List<Pass> passes)
        {
            var result = new ValidationResult();

            // 1. Проверка начальных параметров
            if (init == null || init.W0 <= 0.001)
            {
                result.Messages.Add("Критическая ошибка: Не введена площадь сечения заготовки (W0) в начальных параметрах.");
                result.HasCriticalErrors = true;
                return result;
            }

            // 2. Инициализируем CalculationContext для запуска Analis_data()
            var ctx = new CalculationContext();
            ctx.NPR = passes.Count;
            ctx.W0 = init.W0;
            ctx.K17 = 0; // Проверка без учета расчета формоизменения

            var orderedPasses = passes.OrderBy(x => x.N).ToList();
            for (int i = 0; i < ctx.NPR; i++)
            {
                int idx = i + 1; // Индексация с 1
                var p = orderedPasses[i];
                ctx.KOD0[idx] = p.KOD0;
                ctx.KOD1[idx] = p.KOD1;
                ctx.H0[idx] = p.H0; ctx.B0[idx] = p.B0;
                ctx.H1[idx] = p.H1; ctx.B1[idx] = p.B1;
                ctx.W[idx] = p.W; ctx.S[idx] = p.S;
                ctx.BVR[idx] = p.BVR; ctx.BD[idx] = p.BD;
                ctx.R[idx] = p.R; ctx.ROV[idx] = p.ROV;
            }

            // 3. ЗАПУСКАЕМ АНАЛИЗ ИЗ ЯДРА (Analis_data)
            bool hasContextErrors = ctx.Analis_data();
            if (hasContextErrors || ctx.CalculationErrors.Any())
            {
                result.Messages.AddRange(ctx.CalculationErrors);
            }

            // 4. Построчная UI-диагностика
            foreach (var p in orderedPasses)
            {
                string prefix = $"Проход №{p.N}: ";

                if (p.H0 < 1 || p.B0 < 1 || p.B1 < 1)
                {
                    result.Messages.Add(prefix + "Не введены обязательные параметры H0, B0 или B1.");
                }

                if (p.H1 >= p.H0 && p.H1 > 0)
                {
                    result.Messages.Add(prefix + "Ошибка: Высота после прохода (H1) не может быть больше или равна высоте подката (H0).");
                }

                switch (p.KOD1)
                {
                    case 1: // Гладкая бочка
                        if (p.H1 < 1) result.Messages.Add(prefix + "Не введена высота (H1) для гладкой бочки.");
                        break;

                    case 8:  // Ящичный
                    case 11: // Ребровой
                    case 6:  // Шестиугольный
                        if (p.H1 < 1 || p.S < 0.1 || p.BVR < 1 || p.BD < 0.1)
                        {
                            result.Messages.Add(prefix + "Не заполнены параметры калибра (H1, S, BVR или BD).");
                        }
                        else
                        {
                            if (p.BD > p.BVR) result.Messages.Add(prefix + "Ширина по дну (BD) больше ширины по врезу (BVR).");
                            if (p.R > (p.H1 - p.S) / 2 - 1 && p.R > 0) result.Messages.Add(prefix + "Нарушено условие R < (H1-S)/2 - 1.");
                        }
                        break;

                    case 4: // Овальный
                        if (p.H1 < 1 || p.ROV < 1 || p.S < 0.1 || p.BVR < 1)
                        {
                            result.Messages.Add(prefix + "Неполные данные для овального калибра.");
                        }
                        break;

                    case 2: // Круглый
                        if (p.H1 < 1 || p.S < 0.1 || p.BVR < 1)
                        {
                            result.Messages.Add(prefix + "Неполные данные для круглого калибра.");
                        }
                        else if ((p.S / 2 + p.BVR - p.H1) < 0)
                        {
                            result.Messages.Add(prefix + "Геометрия круга невозможна: (S/2 + BVR - H1) < 0.");
                        }
                        break;

                    case 3: // Квадратный
                        if (p.R > (p.H1 - p.S / 2) * 0.7 || p.R > (p.BVR / 2) * 0.7)
                            result.Messages.Add(prefix + "Радиус R слишком велик для данного квадратного калибра.");
                        break;
                }

                // Предупреждение о переполнении калибра
                if (p.B1 > p.BVR && p.BVR > 0)
                {
                    result.Messages.Add(prefix + "ВНИМАНИЕ: Ширина полосы B1 превышает ширину калибра BVR (возможен перелив/ус).");
                }
            }

            // Убираем дубликаты сообщений (так как UI-проверка и Analis_data могут дублироваться)
            result.Messages = result.Messages.Distinct().ToList();

            // Если есть хоть одно сообщение, которое НЕ содержит слово "ВНИМАНИЕ" или "Скорректировано", значит это критическая ошибка
            result.HasCriticalErrors = result.Messages.Any(m => !m.Contains("ВНИМАНИЕ") && !m.Contains("Скорректировано"));

            return result;
        }
    }
}