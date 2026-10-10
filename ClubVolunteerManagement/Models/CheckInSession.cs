
namespace ClubVolunteerManagement.Models;

public class CheckInSession
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public DateTime OpenedAtUtc { get; set; }
        = DateTime.UtcNow;

    public DateTime? ClosedAtUtc { get; set; }

    public bool IsOpen { get; set; } = true;

    public string? TokenHash { get; set; }

    public DateTime? TokenExpiresAtUtc
    { get; set; }
}