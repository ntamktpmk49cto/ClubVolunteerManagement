namespace ClubVolunteerManagement.Models;

public class EventRegistration
{
    public int Id { get; set; }

    public int EventId { get; set; }
    public Event Event { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    // Chỉ giữ lại 2 thuộc tính này
    public RegistrationStatus Status { get; set; } = RegistrationStatus.Confirmed;
    public DateTime RegisteredAtUtc { get; set; } = DateTime.UtcNow;
}