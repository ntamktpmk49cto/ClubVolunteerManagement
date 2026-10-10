    
using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Cấu hình ASP.NET Core MVC.
builder.Services.AddControllersWithViews();
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
        // Dùng cho bản demo khi chưa có email xác thực.
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

// Xác thực phải đứng trước phân quyền.
app.UseAuthentication();
app.UseAuthorization();

// 7. Đăng ký các đường dẫn MVC.
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Identity sử dụng Razor Pages.
app.MapRazorPages();

app.Run();
