using ClubVolunteerManagement.Data;
using ClubVolunteerManagement.Models;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ClubVolunteerManagement.Services;

public record RegistrationResult(
    bool Success,
    string Message
);

public class EventRegistrationService
{
    private readonly ApplicationDbContext _db;

    public EventRegistrationService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<RegistrationResult> RegisterAsync(int eventId, string userId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        // Khóa dòng sự kiện để tránh xung đột khi nhiều sinh viên bấm đăng ký cùng tích tắc
        var e = await _db.Events
            .FromSqlInterpolated($"SELECT * FROM dbo.Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = {eventId}")
            .SingleOrDefaultAsync();

        if (e == null)
            return new(false, "Không tìm thấy thông tin sự kiện.");

        var now = DateTime.UtcNow;

        // 1. Kiểm tra trạng thái sự kiện: Không thể đăng ký sự kiện đã kết thúc hoặc đã bị hủy
        if (e.Status == EventStatus.Cancelled)
            return new(false, "Sự kiện này đã bị hủy, không thể đăng ký.");

        if (e.Status == EventStatus.Completed || now >= e.EndAtUtc)
            return new(false, "Sự kiện đã kết thúc.");

        if (e.Status != EventStatus.Published)
            return new(false, "Sự kiện chưa được công bố mở đăng ký.");

        if (now >= e.RegistrationDeadlineUtc || now >= e.StartAtUtc)
            return new(false, "Đã hết thời hạn đăng ký sự kiện.");

        // 2. Một sinh viên không thể đăng ký cùng một sự kiện hai lần
        var existing = await _db.EventRegistrations
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);

        if (existing != null && existing.Status != RegistrationStatus.Cancelled)
        {
            return new(false, "Bạn đã đăng ký sự kiện này rồi.");
        }

        // 3. Kiểm tra số lượng người tham gia
        int confirmedCount = await _db.EventRegistrations
            .CountAsync(x => x.EventId == eventId && x.Status == RegistrationStatus.Confirmed);

        if (confirmedCount >= e.Capacity)
        {
            return new(false, $"Sự kiện đã đủ số lượng người tham gia ({confirmedCount}/{e.Capacity}).");
        }

        var newStatus = RegistrationStatus.Confirmed;

        if (existing == null)
        {
            _db.EventRegistrations.Add(new EventRegistration
            {
                EventId = eventId,
                UserId = userId,
                Status = newStatus,
                RegisteredAtUtc = now
            });
        }
        else
        {
            // Trường hợp trước đó đã Hủy nay được đăng ký lại
            existing.Status = newStatus;
            existing.RegisteredAtUtc = now;
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new(true, "Đăng ký tham gia sự kiện thành công!");
    }

    public async Task<RegistrationResult> CancelAsync(int eventId, string userId)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        var e = await _db.Events
            .FromSqlInterpolated($"SELECT * FROM dbo.Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = {eventId}")
            .SingleOrDefaultAsync();

        if (e == null)
            return new(false, "Không tìm thấy sự kiện.");

        if (e.Status != EventStatus.Published ||
            DateTime.UtcNow >= e.RegistrationDeadlineUtc ||
            DateTime.UtcNow >= e.StartAtUtc)
        {
            return new(false, "Đã hết thời gian cho phép hủy đăng ký.");
        }

        var registration = await _db.EventRegistrations
            .FirstOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId);

        if (registration == null || registration.Status == RegistrationStatus.Cancelled)
        {
            return new(false, "Bạn chưa đăng ký sự kiện này.");
        }

        registration.Status = RegistrationStatus.Cancelled;

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new(true, "Hủy đăng ký thành công.");
    }
}