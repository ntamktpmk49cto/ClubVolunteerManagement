
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Identity;

namespace ClubVolunteerManagement.Data.Seed;

public static class IdentitySeeder
{
    public static async Task SeedAdminAsync(
        IServiceProvider services,
        string adminEmail)
    {
        var roleManager = services
            .GetRequiredService<RoleManager<IdentityRole>>();

        var userManager = services
            .GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync("Admin"))
        {
            var result = await roleManager.CreateAsync(
                new IdentityRole("Admin"));

            if (!result.Succeeded)
                throw new Exception("Không tạo được role Admin.");
        }

        var user = await userManager.FindByEmailAsync(adminEmail);

        if (user == null)
            throw new Exception(
                "Không tìm thấy tài khoản: " + adminEmail);

        if (!await userManager.IsInRoleAsync(user, "Admin"))
        {
            var result = await userManager.AddToRoleAsync(
                user, "Admin");

            if (!result.Succeeded)
                throw new Exception("Không gán được quyền Admin.");
        }
    }
}
