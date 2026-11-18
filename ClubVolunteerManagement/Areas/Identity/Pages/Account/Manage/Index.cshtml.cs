#nullable disable
using System.ComponentModel.DataAnnotations;
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClubVolunteerManagement.Areas.Identity.Pages.Account.Manage;

public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public string Username { get; set; }

    [TempData]
    public string StatusMessage { get; set; }

    [BindProperty]
    public InputModel Input { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "Vui lòng nhập họ và tên.")]
        [StringLength(100)]
        [Display(Name = "Họ và tên")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã số sinh viên.")]
        [StringLength(20)]
        [Display(Name = "Mã số sinh viên (MSSV)")]
        public string StudentCode { get; set; }

        [StringLength(150)]
        [Display(Name = "Trường / Khoa trực thuộc")]
        public string SchoolOrFaculty { get; set; }

        [StringLength(100)]
        [Display(Name = "Ngành học")]
        public string Major { get; set; }

        [StringLength(20)]
        [Display(Name = "Khóa")]
        public string Cohort { get; set; }

        [StringLength(30)]
        [Display(Name = "Lớp")]
        public string ClassName { get; set; }

        [Phone(ErrorMessage = "Số điện thoại không hợp lệ.")]
        [Display(Name = "Số điện thoại")]
        public string PhoneNumber { get; set; }
    }

    private async Task LoadAsync(ApplicationUser user)
    {
        var userName = await _userManager.GetUserNameAsync(user);
        var phoneNumber = await _userManager.GetPhoneNumberAsync(user);

        Username = userName;

        Input = new InputModel
        {
            FullName = user.FullName,
            StudentCode = user.StudentCode,
            SchoolOrFaculty = user.SchoolOrFaculty,
            Major = user.Major,
            Cohort = user.Cohort,
            ClassName = user.ClassName,
            PhoneNumber = phoneNumber
        };
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
        }

        await LoadAsync(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return NotFound($"Không thể tải thông tin người dùng với ID '{_userManager.GetUserId(User)}'.");
        }

        if (!ModelState.IsValid)
        {
            await LoadAsync(user);
            return Page();
        }

        var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
        if (Input.PhoneNumber != phoneNumber)
        {
            var setPhoneResult = await _userManager.SetPhoneNumberAsync(user, Input.PhoneNumber);
            if (!setPhoneResult.Succeeded)
            {
                StatusMessage = "Lỗi: Không thể cập nhật số điện thoại.";
                return RedirectToPage();
            }
        }

        user.FullName = Input.FullName?.Trim();
        user.StudentCode = Input.StudentCode?.Trim().ToUpper();
        user.SchoolOrFaculty = Input.SchoolOrFaculty?.Trim();
        user.Major = Input.Major?.Trim();
        user.Cohort = Input.Cohort?.Trim().ToUpper();
        user.ClassName = Input.ClassName?.Trim().ToUpper();

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            StatusMessage = "Lỗi: Không thể lưu thông tin hồ sơ sinh viên.";
            return RedirectToPage();
        }

        await _signInManager.RefreshSignInAsync(user);
        StatusMessage = "Hồ sơ sinh viên của bạn đã được cập nhật thành công!";
        return RedirectToPage();
    }
}