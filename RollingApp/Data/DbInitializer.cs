using Microsoft.AspNetCore.Identity;

using RollingApp.Models.Identity;

namespace RollingApp.Data
{
    public static class DbInitializer
    {
        public static async Task SeedRolesAndSuperAdminAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            string[] roleNames = { "Admin", "Operator" };

            foreach (var roleName in roleNames)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            var superAdminEmail = "admin@mill.local";
            var superAdmin = await userManager.FindByEmailAsync(superAdminEmail);


            // -- КОД НИЖЕ СОЗДАЕТ СУПЕРАДМИНА В ПУСТОЙ БД -- //
            //if (superAdmin == null)
            //{
            //    var newAdmin = new ApplicationUser
            //    {
            //        UserName = "admin",
            //        Email = superAdminEmail,
            //        FullName = "Администратор",
            //        EmailConfirmed = true
            //    };

            //    var result = await userManager.CreateAsync(newAdmin, "SuperAdmin123!");
            //    if (result.Succeeded)
            //    {
            //        await userManager.AddToRoleAsync(newAdmin, "Admin");
            //    }
            //}
        }
    }
}