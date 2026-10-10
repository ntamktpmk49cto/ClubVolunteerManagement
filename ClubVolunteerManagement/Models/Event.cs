
using System.ComponentModel.DataAnnotations;

namespace ClubVolunteerManagement.Models;

public class Event
{
    public int Id { get; set; }

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(3000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(250)]
    public string Location { get; set; } = string.Empty;

    public EventType Type { get; set; }
        = EventType.ClubMeeting;

    public EventStatus Status { get; set; }
        = EventStatus.Draft;

    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }

    public DateTime RegistrationDeadlineUtc
    { get; set; }

    public int Capacity { get; set; }

    public decimal VolunteerHours { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public ICollection<EventRegistration> Registrations
    { get; set; } = new List<EventRegistration>();

    public ICollection<CheckInSession> CheckInSessions
    { get; set; } = new List<CheckInSession>();
}