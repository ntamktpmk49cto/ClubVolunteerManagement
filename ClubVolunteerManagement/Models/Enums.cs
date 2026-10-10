
namespace ClubVolunteerManagement.Models;

public enum ClubRole
{
    Member = 1,
    Manager = 2
}

public enum ClubMemberStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}

public enum EventType
{
    ClubMeeting = 1,
    Volunteer = 2,
    Workshop = 3
}

public enum EventStatus
{
    Draft = 1,
    PendingApproval = 2,
    Published = 3,
    InProgress = 4,
    Completed = 5,
    Cancelled = 6
}

public enum RegistrationStatus
{
    Confirmed = 1,
    Waitlisted = 2,
    Cancelled = 3
}