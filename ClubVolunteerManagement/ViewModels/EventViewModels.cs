
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ClubVolunteerManagement.ViewModels;

public class EventFormViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn CLB")]
    public int ClubId { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên sự kiện")]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(3000)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa điểm")]
    [StringLength(250)]
    public string Location { get; set; } = string.Empty;

    public EventType Type { get; set; }

    [Required]
    [Display(Name = "Thời gian bắt đầu")]
    public DateTime StartAtLocal { get; set; }

    [Required]
    [Display(Name = "Thời gian kết thúc")]
    public DateTime EndAtLocal { get; set; }

    [Required]
    [Display(Name = "Hạn đăng ký")]
    public DateTime RegistrationDeadlineLocal { get; set; }

    [Range(1, 10000,
        ErrorMessage = "Số lượng phải từ 1 đến 10000")]
    public int Capacity { get; set; } = 30;

    [Range(typeof(decimal), "0", "999.99")]
    public decimal VolunteerHours { get; set; }

    public List<SelectListItem> ClubOptions
    { get; set; } = new();
}

public class EventsIndexViewModel
{
    public List<Event> Events { get; set; } = new();

    public bool IsAdmin { get; set; }

    public HashSet<int> ManagedClubIds
    { get; set; } = new();
}

public class EventDetailsViewModel
{
    public Event Event { get; set; } = null!;

    public int ConfirmedCount { get; set; }

    public RegistrationStatus? MyRegistrationStatus
    { get; set; }

    public bool CanManage { get; set; }

    public bool IsAdmin { get; set; }
}
