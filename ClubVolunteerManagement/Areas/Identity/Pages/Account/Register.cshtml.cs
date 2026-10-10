
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ClubVolunteerManagement.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public IList<AuthenticationScheme> ExternalLogins
        { get; set; } = new List<AuthenticationScheme>();

        // Thông tin sinh viên nhập khi đăng ký
        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
            [StringLength(120, MinimumLength = 2,
                ErrorMessage = "Họ tên phải từ 2 đến 120 ký tự")]
            [Display(Name = "Họ và tên")]
            public string FullName { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mã số sinh viên")]
            [StringLength(20, MinimumLength = 4,
                ErrorMessage = "MSSV phải từ 4 đến 20 ký tự")]
            [Display(Name = "Mã số sinh viên")]
            public string StudentCode { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập email")]
            [EmailAddress(ErrorMessage = "Email không hợp lệ")]
            [Display(Name = "Email")]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
            [StringLength(100, MinimumLength = 8,
                ErrorMessage = "Mật khẩu phải có ít nhất 8 ký tự")]
            [DataType(DataType.Password)]
            [Display(Name = "Mật khẩu")]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
            [DataType(DataType.Password)]
            [Display(Name = "Xác nhận mật khẩu")]
            [Compare("Password",
                ErrorMessage = "Mật khẩu xác nhận không khớp")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        // Hiển thị trang đăng ký
        public async Task OnGetAsync(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            ExternalLogins = (
                await _signInManager.GetExternalAuthenticationSchemesAsync()
            ).ToList();
        }

        // Xử lý khi nhấn nút Đăng ký
        public async Task<IActionResult> OnPostAsync(
            string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            ExternalLogins = (
                await _signInManager.GetExternalAuthenticationSchemesAsync()
            ).ToList();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Chuẩn hóa MSSV
            string studentCode =
                Input.StudentCode.Trim().ToUpperInvariant();

            // Kiểm tra MSSV đã tồn tại chưa
            bool studentCodeExists = await _userManager.Users
                .AnyAsync(u => u.StudentCode == studentCode);

            if (studentCodeExists)
            {
                ModelState.AddModelError(
                    "Input.StudentCode",
                    "Mã số sinh viên này đã được đăng ký");

                return Page();
            }

            // Tạo tài khoản sinh viên
            var user = new ApplicationUser
            {
                FullName = Input.FullName.Trim(),
                StudentCode = studentCode,
                CreatedAtUtc = DateTime.UtcNow
            };

            // Gán tên đăng nhập và Email
            await _userStore.SetUserNameAsync(
                user,
                Input.Email.Trim(),
                CancellationToken.None);

            await _emailStore.SetEmailAsync(
                user,
                Input.Email.Trim(),
                CancellationToken.None);

            // Tạo tài khoản với mật khẩu được Identity xử lý
            var result = await _userManager.CreateAsync(
                user,
                Input.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "New student account registered.");

                // Chỉ gửi email khi bật yêu cầu xác nhận.
                if (_userManager.Options.SignIn.RequireConfirmedAccount)
                {
                    var userId =
                        await _userManager.GetUserIdAsync(user);

                    var code =
                        await _userManager.GenerateEmailConfirmationTokenAsync(user);

                    code = WebEncoders.Base64UrlEncode(
                        Encoding.UTF8.GetBytes(code));

                    var callbackUrl = Url.Page(
                        "/Account/ConfirmEmail",
                        pageHandler: null,
                        values: new
                        {
                            area = "Identity",
                            userId,
                            code,
                            returnUrl
                        },
                        protocol: Request.Scheme);

                    await _emailSender.SendEmailAsync(
                        user.Email!,
                        "Xác nhận tài khoản",
                        $"Vui lòng xác nhận tài khoản: " +
                        $"<a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>" +
                        "Xác nhận Email</a>");

                    return RedirectToPage(
                        "RegisterConfirmation",
                        new { email = user.Email, returnUrl });
                }

                // Đăng nhập ngay sau khi đăng ký thành công.
                await _signInManager.SignInAsync(
                    user,
                    isPersistent: false);

                return LocalRedirect(returnUrl);
            }

            // Hiển thị lỗi do Identity trả về.
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty,
                    error.Description);
            }

            return Page();
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException(
                    "User store does not support email.");
            }

            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
