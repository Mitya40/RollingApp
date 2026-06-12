using RollingApp.Models.Base;

using System.ComponentModel.DataAnnotations.Schema;

namespace RollingApp.Models.Domain
{
    /// <summary>
    /// Клеть стана (Оборудование)
    /// </summary>
    public class Stand : BaseEntity
    {
        public int MillId { get; set; }
        [ForeignKey("MillId")]
        public Mill? Mill { get; set; }

        /// <summary> Номер клети по порядку в стане </summary>
        public int SequenceNumber { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary> DB: Диаметр бочки валка </summary>
        public double DB { get; set; }

        /// <summary> DSH: Диаметр шейки валка </summary>
        public double DSH { get; set; }

        /// <summary> MU: Состояние поверхности валков </summary>
        public double MU { get; set; }

        /// <summary> FPOD: Коэфф. трения в подшипниках </summary>
        public double FPOD { get; set; }

        /// <summary> LKL: Расстояние между клетями </summary>
        public double LKL { get; set; }

        /// <summary> AKL: Расстояние между станинами </summary>
        public double AKL { get; set; }

        /// <summary> IR: Передаточное отношение редуктора </summary>
        public double IR { get; set; }

        /// <summary> ETA: КПД линии привода </summary>
        public double ETA { get; set; }

        /// <summary> NZ: Опытная частота вращения валков </summary>
        public double NZ { get; set; }

        /// <summary> NNOM: Номинальная мощность двигателя </summary>
        public double NNOM { get; set; }

        /// <summary> PP: Признак привода </summary>
        public double PP { get; set; }

        /// <summary> NDVN: Номинальная частота двигателя </summary>
        public double NDVN { get; set; }

        /// <summary> NDVMIN: Минимальная частота двигателя </summary>
        public double NDVMIN { get; set; }

        /// <summary> NDVMAX: Максимальная частота двигателя </summary>
        public double NDVMAX { get; set; }

        /// <summary> PDOP: Допустимое усилие прокатки </summary>
        public double PDOP { get; set; }

        /// <summary> MDOP: Допустимый крутящий момент </summary>
        public double MDOP { get; set; }

        /// <summary> C: Коэффициент жесткости </summary>
        public double C { get; set; }
    }
}