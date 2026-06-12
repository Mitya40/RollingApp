using System.ComponentModel.DataAnnotations;

namespace RollingApp.Models.Base
{
    /// <summary>
    /// Базовый класс для всех сущностей с аудитом
    /// </summary>
    public abstract class BaseEntity
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// ID пользователя, создавшего запись
        /// </summary>
        public string CreatorId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string? ModifierId { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}