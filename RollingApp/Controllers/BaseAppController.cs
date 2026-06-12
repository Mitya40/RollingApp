using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RollingApp.Models.Base;

namespace RollingApp.Controllers.Base
{
    public abstract class BaseAppController : Controller
    {
        protected bool IsUserAuthorizedToEdit(BaseEntity entity)
        {
            if (User.IsInRole("Admin")) return true;

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return entity.CreatorId == currentUserId;
        }

        protected string GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        }
    }
}