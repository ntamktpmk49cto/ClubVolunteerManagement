
namespace ClubVolunteerManagement.Models;

public class EventRegistration
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public RegistrationStatus Status { get; set; }
        = RegistrationStatus.Confirmed;

    public DateTime RegisteredAtUtc { get; set; }
        = DateTime.UtcNow;
}