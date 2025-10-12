using Microsoft.AspNetCore.Identity;
using TaskManager.Api.Models;

namespace TaskManager.Api.Data
{
    public static class IdentitySeeder
    {
        public static async Task SeedAsync(IServiceProvider sp, IConfiguration cfg)
        {
            using var scope = sp.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

            const string adminRole = "Admin";
            if (!await roleManager.RoleExistsAsync(adminRole))
            {
                await roleManager.CreateAsync(new IdentityRole(adminRole));
            }

            const string userRole = "User";
            if (!await roleManager.RoleExistsAsync(userRole))
            {
                await roleManager.CreateAsync(new IdentityRole(userRole));
            }

            const string teamLeadRole = "TeamLead";
            if (!await roleManager.RoleExistsAsync(teamLeadRole))
            {
                await roleManager.CreateAsync(new IdentityRole(teamLeadRole));
            }

            var adminEmail = cfg["Admin:Email"];
            var adminPassword = cfg["Admin:Password"];
            var adminDisplay = cfg["Admin:DisplayName"] ?? "System Admin";

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
                return; // секреты не заданы — пропускаем сид

            var admin = await userManager.FindByEmailAsync(adminEmail);
            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    DisplayName = adminDisplay,
                    EmailConfirmed = true
                };
                var create = await userManager.CreateAsync(admin, adminPassword);
                if (!create.Succeeded) return;

                admin = await userManager.FindByEmailAsync(adminEmail);
            }

            if (admin != null && !await userManager.IsInRoleAsync(admin, adminRole))
            {
                var add = await userManager.AddToRoleAsync(admin, adminRole);
                if (!add.Succeeded)
                {
                    // логируем или выбрасываем исключение — чтобы видеть если что-то пошло не так
                    throw new Exception("Не удалось добавить пользователя в роль Admin: " +
                                        string.Join(", ", add.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
