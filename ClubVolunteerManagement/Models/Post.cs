using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ClubVolunteerManagement.Models;

public class Post
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public virtual ApplicationUser? User { get; set; }

    // Thêm liên kết tới Sự kiện (nếu bài đăng này gắn liền với 1 sự kiện)
    public int? EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public virtual Event? Event { get; set; }

    [Required(ErrorMessage = "Nội dung bài đăng không được để trống.")]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}