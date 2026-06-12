using RollingApp.Models.Base;

namespace RollingApp.Models.Domain
{
    /// <summary>
    /// Прокатный стан
    /// </summary>
    public class Mill : BaseEntity
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }

        public ICollection<Stand> Stands { get; set; } = new List<Stand>();
        public ICollection<Profile> Profiles { get; set; } = new List<Profile>();
    }
}