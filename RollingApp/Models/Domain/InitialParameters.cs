using RollingApp.Models.Base;

using System.ComponentModel.DataAnnotations.Schema;

namespace RollingApp.Models.Domain
{
    public class InitialParameter : BaseEntity
    {

        public int ProfileId { get; set; }
        [ForeignKey("ProfileId")]
        public Profile? Profile { get; set; }
        public DateTime TargetDate { get; set; }
        public double W0 { get; set; } // ПЛОЩАДЬ ПОПЕРЕЧНОГО СЕЧЕНИЯ ЗАГОТОВКИ
        public double P0 { get; set; } // ПЕРИМЕТР ЗАГОТОВКИ
        public double L0 { get; set; } // ДЛИНА ЗАГОТОВКИ
        public double T0 { get; set; } // НАЧАЛЬНАЯ ТЕМПЕРАТУРА ПОДКАТА
        public double TAU { get; set; } // ВРЕМЯ ДВИЖЕНИЯ ЗАГОТОВКИ ОТ ПЕЧИ К СТАНУ
        public double LR { get; set; } // ЗАПАС ЧАСТОТЫ ВРАЩЕНИЯ
        public double VK { get; set; } // КОНЕЧНАЯ СКОРОСТЬ ПРОКАТКИ
    }
}