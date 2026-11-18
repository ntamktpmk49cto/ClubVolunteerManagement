
namespace ClubVolunteerManagement.ViewModels;

// Một hoạt động tình nguyện và trạng thái chứng nhận.
public class CertificateListItemViewModel
{
    public int AttendanceId { get; set; }

    public string EventTitle { get; set; } = string.Empty;

    public string ClubName { get; set; } = string.Empty;

    public DateTime EventStartAtUtc { get; set; }

    public DateTime EventEndAtUtc { get; set; }

    public decimal Hours { get; set; }

    public int? CertificateId { get; set; }

    public bool IsRevoked { get; set; }

    public bool CanIssue { get; set; }
}

// Dữ liệu hiển thị khi người dùng tra cứu chứng nhận.
public class CertificateVerifyViewModel
{
    public string VerificationCode { get; set; } = string.Empty;

    public bool Found { get; set; }

    public bool IsValid { get; set; }

    public string RecipientName { get; set; } = string.Empty;

    public string EventTitle { get; set; } = string.Empty;

    public string ClubName { get; set; } = string.Empty;

    public decimal Hours { get; set; }

    public DateTime IssuedAtUtc { get; set; }
}
