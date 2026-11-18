namespace ClubVolunteerManagement.ViewModels
{
    // Class tổng chứa thông tin sự kiện và danh sách sinh viên
    public class EventAttendanceViewModel
    {
        public int EventId { get; set; }
        public string EventTitle { get; set; }
        public int TotalRegistered { get; set; }
        public int TotalCheckedIn { get; set; }
        public List<AttendanceStudentViewModel> Students { get; set; } = new List<AttendanceStudentViewModel>();
    }

    // Class đại diện cho từng dòng (từng sinh viên) trong bảng
    public class AttendanceStudentViewModel
    {
        public string StudentCode { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool IsCheckedIn { get; set; }
    }
}