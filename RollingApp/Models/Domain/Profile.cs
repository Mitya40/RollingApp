using RollingApp.Models.Base;
using System.ComponentModel.DataAnnotations.Schema;

namespace RollingApp.Models.Domain
{
    /// <summary>
    /// Профиль проката
    /// </summary>
    public class Profile : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public DateTime CreationDate { get; set; }

        public int MillId { get; set; }
        [ForeignKey("MillId")]
        public Mill? Mill { get; set; }

        public int? SteelId { get; set; }
        [ForeignKey("SteelId")]
        public Steel? Steel { get; set; }

        /// <summary> Является ли профиль эталонной схемой проката </summary>
        public bool IsTemplate { get; set; }

        /// <summary> Ссылка на эталонный профиль (если это корректировка) </summary>
        public int? ParentProfileId { get; set; }
        [ForeignKey("ParentProfileId")]
        public Profile? ParentProfile { get; set; }

        public ICollection<Pass> Passes { get; set; } = new List<Pass>();

        // Связь один-к-одному с начальными параметрами
        public InitialParameter? InitialParameter { get; set; }
    }
}