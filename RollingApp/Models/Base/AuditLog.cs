using System.ComponentModel.DataAnnotations;

namespace RollingApp.Models.Base
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // Create, Update, Delete
        public string TableName { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public string OldValues { get; set; } = string.Empty; // JSON
        public string NewValues { get; set; } = string.Empty; // JSON
        public string PrimaryKey { get; set; } = string.Empty;
    }
}