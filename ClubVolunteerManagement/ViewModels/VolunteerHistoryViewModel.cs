
using System;
using System.Collections.Generic;

namespace ClubVolunteerManagement.ViewModels
{
    // Một dòng trong lịch sử giờ tình nguyện.
    public class VolunteerHourItemViewModel
    {
        public int EventId { get; set; }

        public string EventTitle { get; set; }
            = string.Empty;

        public string ClubName { get; set; }
            = string.Empty;

        public string Location { get; set; }
            = string.Empty;

        public DateTime EventStartAtUtc { get; set; }

        public DateTime GrantedAtUtc { get; set; }

        public decimal Hours { get; set; }
    }

    // Hồ sơ tổng hợp của một tình nguyện viên.
    public class VolunteerHistoryViewModel
    {
        public string FullName { get; set; }
            = string.Empty;

        public string StudentCode { get; set; }
            = string.Empty;

        public string Email { get; set; }
            = string.Empty;

        public decimal TotalHours { get; set; }

        public int ActivityCount { get; set; }

        public int CertificateCount { get; set; }

        public List<VolunteerHourItemViewModel> Activities
        { get; set; } = new List<VolunteerHourItemViewModel>();
    }
}
