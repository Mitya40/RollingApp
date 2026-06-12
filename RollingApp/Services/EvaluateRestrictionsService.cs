using RollingApp.Services;

using System.Collections.Generic;
using System.Linq;

namespace RollingApp.Services
{
    // Класс для группировки результатов
    public class ExpertReport
    {
        public string Category { get; set; } = string.Empty;
        public List<string> Warnings { get; set; } = new();
        public bool IsPassed => !Warnings.Any();
    }

    public class EvaluateRestrictionsService
    {
        /// <summary>
        /// Оценка результатов моделирования на ограничения
        /// </summary>
        public List<ExpertReport> EvaluateCalculationResults(CalculationContext ctx)
        {
            var reports = new List<ExpertReport>
            {
                new ExpertReport { Category = "1. Условия захвата металла валками" },
                new ExpertReport { Category = "2. Условия устойчивости полосы в калибрах" },
                new ExpertReport { Category = "3. Скоростной режим прокатки" },
                new ExpertReport { Category = "4. Степень заполнения калибров" },
                new ExpertReport { Category = "5. Нагруженность оборудования (Усилие)" },
                new ExpertReport { Category = "6. Нагруженность оборудования (Момент)" },
                new ExpertReport { Category = "7. Загрузка электродвигателей" }
            };

            for (int i = 1; i <= ctx.NPR; i++)
            {
                string passPrefix = $"Клеть {ctx.N[i]} (Проход {i}): ";

                // 1. Угол захвата
                if (ctx.UGZAX[i] >= ctx.ALFA[i])
                    reports[0].Warnings.Add(passPrefix + $"Угол захвата ({ctx.UGZAX[i]:F2}°) превышает допустимый ({ctx.ALFA[i]:F2}°). Возможна пробуксовка.");

                // 2. Устойчивость (отношение осей)
                if (ctx.A0[i] >= ctx.ADOP[i])
                    reports[1].Warnings.Add(passPrefix + $"Отношение осей ({ctx.A0[i]:F2}) больше допустимого ({ctx.ADOP[i]:F2}). Риск сваливания/скручивания полосы.");

                // 3. Скоростной режим
                if (ctx.NR[i] < ctx.NMIN[i] || ctx.NR[i] > ctx.NMAX[i])
                    reports[2].Warnings.Add(passPrefix + $"Рабочая частота ({ctx.NR[i]:F1} об/мин) выходит за пределы двигателей[{ctx.NMIN[i]:F1} ... {ctx.NMAX[i]:F1}].");

                // 4. Заполнение калибров (Используем DELRP по выпуску/врезу)
                if (ctx.DELRP[i] > 1.01)
                    reports[3].Warnings.Add(passPrefix + $"Переполнение калибра. Степень заполнения: {ctx.DELRP[i]:F2}. Риск образования усов.");
                else if (ctx.DELRP[i] < 0.6)
                    reports[3].Warnings.Add(passPrefix + $"Недостаточное заполнение калибра ({ctx.DELRP[i]:F2} < 0.60). Искажение формы.");

                // 5. Прочность по усилию (Коэфф. запаса KPS)
                if (ctx.KPS[i] > 1.0)
                    reports[4].Warnings.Add(passPrefix + $"Перегрузка по усилию прокатки! Загрузка: {ctx.KPS[i] * 100:F1}%. Возможна поломка валков.");

                // 6. Прочность по крутящему моменту
                if (ctx.KMS[i] > 1.0)
                    reports[5].Warnings.Add(passPrefix + $"Перегрузка линии привода по моменту! Загрузка: {ctx.KMS[i] * 100:F1}%. Риск поломки шпинделей/редуктора.");

                // 7. Загрузка электродвигателя
                if (ctx.KDVS[i] > 1.0)
                    reports[6].Warnings.Add(passPrefix + $"Перегрузка электродвигателя! Загрузка: {ctx.KDVS[i] * 100:F1}%. Возможна остановка стана по защите.");
            }

            return reports;
        }
    }
}