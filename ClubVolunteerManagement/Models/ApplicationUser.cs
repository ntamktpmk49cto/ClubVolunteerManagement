using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ClubVolunteerManagement.Models;

public class ApplicationUser : IdentityUser
{
    [StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string? FullName { get; set; }

    [StringLength(20)]
    [Display(Name = "Mã số sinh viên")]
    public string? StudentCode { get; set; }

    [StringLength(150)]
    [Display(Name = "Trường / Khoa trực thuộc")]
    public string? SchoolOrFaculty { get; set; }

    [StringLength(100)]
    [Display(Name = "Ngành học")]
    public string? Major { get; set; }

    [StringLength(20)]
    [Display(Name = "Khóa")]
    public string? Cohort { get; set; }

    [StringLength(30)]
    [Display(Name = "Lớp")]
    public string? ClassName { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<ClubMember> ClubMemberships { get; set; } = new List<ClubMember>();
}