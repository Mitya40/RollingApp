using RollingApp.Models.Base;

namespace RollingApp.Models.Domain
{
    /// <summary>
    /// Марка стали и её коэффициенты для расчетов
    /// </summary>
    public class Steel : BaseEntity
    {
        public string Name { get; set; } = string.Empty;

        /// <summary> Код материала (1 - углеродистая/низколегированная, 1.4 - высокоуглеродистая/легированная) </summary>
        public double M { get; set; }

        // Коэффициенты для вычисления сопротивления деформации (K1-K9)
        public double K1 { get; set; }
        public double K2 { get; set; }
        public double K3 { get; set; }
        public double K4 { get; set; }
        public double K5 { get; set; }
        public double K6 { get; set; }
        public double K7 { get; set; }
        public double K8 { get; set; }
        public double K9 { get; set; }
    }
}