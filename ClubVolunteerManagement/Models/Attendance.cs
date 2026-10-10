
namespace ClubVolunteerManagement.Models;

public class Attendance
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public int CheckInSessionId { get; set; }

    public CheckInSession CheckInSession
    { get; set; } = null!;

    public DateTime CheckedInAtUtc { get; set; }
        = DateTime.UtcNow;

    public bool IsValid { get; set; } = true;
}