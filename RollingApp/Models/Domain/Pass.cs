using RollingApp.Models.Base;

using System.ComponentModel.DataAnnotations.Schema;

namespace RollingApp.Models.Domain
{
    /// <summary>
    /// Проход (Технология прокатки)
    /// </summary>
    public class Pass : BaseEntity
    {
        public int ProfileId { get; set; }
        [ForeignKey("ProfileId")]
        public Profile? Profile { get; set; }

        /// <summary> Ссылка на физическую клеть стана, в которой идет проход </summary>
        public int StandId { get; set; }
        [ForeignKey("StandId")]
        public Stand? Stand { get; set; }

        /// <summary> N: Номер прохода по порядку </summary>
        public int N { get; set; }

        /// <summary> KOD0 (SXEMAB): Схема подката </summary>
        public int KOD0 { get; set; }

        /// <summary> KOD1 (SXEMAE): Схема калибра </summary>
        public int KOD1 { get; set; }

        /// <summary> H0: Высота подката </summary>
        public double H0 { get; set; }

        /// <summary> B0: Ширина подката </summary>
        public double B0 { get; set; }

        /// <summary> H1: Высота полосы/калибра </summary>
        public double H1 { get; set; }

        /// <summary> B1: Ширина полосы/калибра </summary>
        public double B1 { get; set; }

        /// <summary> W: Площадь поперечного сечения </summary>
        public double W { get; set; }

        /// <summary> S: Зазор валков </summary>
        public double S { get; set; }

        /// <summary> BVR: Ширина калибра по врезу </summary>
        public double BVR { get; set; }

        /// <summary> BD: Ширина калибра по дну </summary>
        public double BD { get; set; }

        /// <summary> R: Радиус калибра в вершине </summary>
        public double R { get; set; }

        /// <summary> ROV: Радиус овального калибра </summary>
        public double ROV { get; set; }

        /// <summary> R8: Вогнутость дна калибров </summary>
        public double R8 { get; set; }

        /// <summary> SUMX: Расположение калибров по бочке </summary>
        public double SUMX { get; set; }

        /// <summary> PSI: Коэфф. плеча приложения усилия прокатки </summary>
        public double PSI { get; set; }

        /// <summary> Z: Число ниток </summary>
        public int Z { get; set; }

        /// <summary> SP: Способ пересчета соответственных полос </summary>
        public int SP { get; set; }

        /// <summary> TOP: Опытная температура на прокатном стане </summary>
        public double TOP { get; set; }
    }
}