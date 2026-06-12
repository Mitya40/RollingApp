namespace RollingApp.Models.Calculation
{
    public class CalculationSettings
    {
        public int CalculationMethod { get; set; }
        public int K10 { get; set; }
        public int K11 { get; set; }
        public int K12 { get; set; } // 1 - заданы, 0 - расчетные
        public int K13 { get; set; } // 9 - опытный, 7 - расчетный
        public int K14 { get; set; } // 8 - заданы, 9 - расчетные
        public int K15 { get; set; } // 10 - заданы, 11 - расчетные
        public int K16 { get; set; } // Заполнение калибров
        public int K17 { get; set; } // 1 - да (расчет формоизменения), 0 - нет
        public int TS { get; set; }  // 1 - непрерывный, 2 - последовательный, 3 - с петлевой
    }
}