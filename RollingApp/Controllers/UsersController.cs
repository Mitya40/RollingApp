using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using RollingApp.Models.Identity;

namespace RollingApp.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var userList = new List<UserViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userList.Add(new UserViewModel
                {
                    Id = user.Id,
                    UserName = user.UserName,
                    Email = user.Email ?? "",
                    FullName = user.FullName,
                    Role = roles.FirstOrDefault() ?? "Operator"
                });
            }

            return View(userList);
        }

        [HttpPost]
        public async Task<IActionResult> SaveUser([FromBody] UserSaveDto data)
        {
            if (string.IsNullOrWhiteSpace(data.UserName) || string.IsNullOrWhiteSpace(data.Email))
                return Json(new { success = false, message = "Логин и Email обязательны" });

            ApplicationUser user;
            bool isNew = string.IsNullOrEmpty(data.Id);

            if (isNew)
            {
                if (string.IsNullOrWhiteSpace(data.Password))
                    return Json(new { success = false, message = "Пароль обязателен для нового пользователя" });

                user = new ApplicationUser { UserName = data.UserName, Email = data.Email, FullName = data.FullName, EmailConfirmed = true };
                var result = await _userManager.CreateAsync(user, data.Password);
                if (!result.Succeeded)
                    return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            else
            {
                user = await _userManager.FindByIdAsync(data.Id);
                if (user == null) return Json(new { success = false, message = "Пользователь не найден" });

                // Защита: нельзя менять роль/Email суперадмина через этот интерфейс
                if (user.Email == "admin@mill.local" && (data.Role != "Admin" || data.Email != user.Email))
                    return Json(new { success = false, message = "Запрещено изменять базовые параметры суперадмина" });

                user.Email = data.Email;
                user.UserName = data.UserName;
                user.FullName = data.FullName;

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                    return Json(new { success = false, message = "Ошибка при обновлении данных" });

                // Обновление пароля, если он введен
                if (!string.IsNullOrWhiteSpace(data.Password))
                {
                    var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                    await _userManager.ResetPasswordAsync(user, token, data.Password);
                }
            }

            // Обновление роли
            var currentRoles = await _userManager.GetRolesAsync(user);

            // Проверка на понижение роли последнего админа
            if (currentRoles.Contains("Admin") && data.Role != "Admin")
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                if (admins.Count <= 1)
                    return Json(new { success = false, message = "Нельзя лишить прав последнего администратора" });
            }

            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, data.Role);

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser([FromBody] string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return Json(new { success = false, message = "Пользователь не найден" });

            if (user.Email == "admin@mill.local")
                return Json(new { success = false, message = "Запрещено удалять суперадмина" });

            if (await _userManager.IsInRoleAsync(user, "Admin"))
            {
                var admins = await _userManager.GetUsersInRoleAsync("Admin");
                if (admins.Count <= 1)
                    return Json(new { success = false, message = "Нельзя удалить последнего администратора" });
            }

            await _userManager.DeleteAsync(user);
            return Json(new { success = true });
        }
    }

    public class UserViewModel
    {
        public string Id { get; set; } = "";
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
    }

    public class UserSaveDto
    {
        public string? Id { get; set; }
        public string UserName { get; set; } = "";
        public string Email { get; set; } = "";
        public string FullName { get; set; } = "";
        public string Role { get; set; } = "";
        public string? Password { get; set; }
    }
}