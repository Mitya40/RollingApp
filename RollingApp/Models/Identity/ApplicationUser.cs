using Microsoft.AspNetCore.Identity;

namespace RollingApp.Models.Identity
{
    public class ApplicationUser : IdentityUser
    {
        /// <summary>
        /// ФИО пользователя
        /// </summary>
        public string FullName { get; set; } = string.Empty;
    }
}