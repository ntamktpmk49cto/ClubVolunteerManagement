
namespace ClubVolunteerManagement.Models;

public class VolunteerHour
{
    public int Id { get; set; }

    public int AttendanceId { get; set; }
    public Attendance Attendance { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public decimal Hours { get; set; }

    public DateTime GrantedAtUtc { get; set; }
        = DateTime.UtcNow;
}