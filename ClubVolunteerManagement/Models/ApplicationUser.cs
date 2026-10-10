
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace ClubVolunteerManagement.Models;

public class ApplicationUser : IdentityUser
{
    [MaxLength(120)]
    public string? FullName { get; set; }

    [MaxLength(20)]
    public string? StudentCode { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public ICollection<ClubMember> ClubMemberships
    { get; set; } = new List<ClubMember>();
}