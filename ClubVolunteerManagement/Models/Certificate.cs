
using System.ComponentModel.DataAnnotations;

namespace ClubVolunteerManagement.Models;

public class Certificate
{
    public int Id { get; set; }

    public int AttendanceId { get; set; }
    public Attendance Attendance { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    [Required]
    [MaxLength(64)]
    public string VerificationCode { get; set; }
        = string.Empty;

    public DateTime IssuedAtUtc { get; set; }
        = DateTime.UtcNow;

    public bool IsRevoked { get; set; } = false;
}