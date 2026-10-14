
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using ClubVolunteerManagement.Data.Seed;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ClubVolunteerManagement.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình ASP.NET Core MVC.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<EventRegistrationService>();
builder.Services.AddRazorPages();

// 2. Đọc chuỗi kết nối SQL Server.
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException(
        "Connection string not found.");

// 3. Đăng ký Entity Framework Core.
builder.Services.AddDbContext<ApplicationDbContext>(
    options => options.UseSqlServer(connectionString));

// 4. Cấu hình ASP.NET Core Identity.
builder.Services
    .AddDefaultIdentity<ApplicationUser>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;

        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;

        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan =
            TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

var app = builder.Build();

// 5. Xử lý lỗi.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// 6. Middleware.
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// 7. Đăng ký các đường dẫn MVC.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// 8. Identity sử dụng Razor Pages.
app.MapRazorPages();

// 9. Gán quyền Admin trong môi trường Development.
if (app.Environment.IsDevelopment())
{
    var adminEmail =
        app.Configuration["SeedAdminEmail"];

    if (!string.IsNullOrWhiteSpace(adminEmail))
    {
        using var scope = app.Services.CreateScope();

        await IdentitySeeder.SeedAdminAsync(
            scope.ServiceProvider,
            adminEmail);
    }
}

// 10. Chạy ứng dụng.
app.Run();
