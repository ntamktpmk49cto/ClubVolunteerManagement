
namespace ClubVolunteerManagement.Models;

public class ClubMember
{
    public int Id { get; set; }

    public int ClubId { get; set; }
    public Club Club { get; set; } = null!;

    public string UserId { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;

    public ClubRole Role { get; set; }
        = ClubRole.Member;

    public ClubMemberStatus Status { get; set; }
        = ClubMemberStatus.Pending;

    public DateTime JoinedAtUtc { get; set; }
        = DateTime.UtcNow;
}
