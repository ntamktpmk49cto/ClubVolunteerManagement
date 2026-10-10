
using System.ComponentModel.DataAnnotations;

namespace ClubVolunteerManagement.Models;

public class Club
{
    public int Id { get; set; }

    [Required]
    [MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? LogoUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; }
        = DateTime.UtcNow;

    public ICollection<ClubMember> Members
    { get; set; } = new List<ClubMember>();

    public ICollection<Event> Events
    { get; set; } = new List<Event>();
}