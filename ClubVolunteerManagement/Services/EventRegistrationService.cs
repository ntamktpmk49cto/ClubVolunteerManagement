
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

    public async Task<RegistrationResult> RegisterAsync(
        int eventId, string userId)
    {
        // Transaction tránh nhận quá số chỗ khi
        // nhiều sinh viên đăng ký cùng lúc.
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        // Khóa hàng sự kiện trong thời gian đăng ký.
        var e = await _db.Events
            .FromSqlInterpolated(
                $"SELECT * FROM dbo.Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = {eventId}")
            .SingleOrDefaultAsync();

        if (e == null)
            return new(false, "Không tìm thấy sự kiện");

        var now = DateTime.UtcNow;

        if (e.Status != EventStatus.Published)
            return new(false, "Sự kiện chưa mở đăng ký");

        if (now >= e.RegistrationDeadlineUtc ||
            now >= e.StartAtUtc)
            return new(false, "Đã hết hạn đăng ký");

        var existing = await _db.EventRegistrations
            .FirstOrDefaultAsync(x =>
                x.EventId == eventId &&
                x.UserId == userId);

        if (existing != null &&
            existing.Status != RegistrationStatus.Cancelled)
        {
            return new(false, "Bạn đã đăng ký sự kiện này");
        }

        int confirmedCount = await _db.EventRegistrations
            .CountAsync(x =>
                x.EventId == eventId &&
                x.Status == RegistrationStatus.Confirmed);

        var newStatus = confirmedCount < e.Capacity
            ? RegistrationStatus.Confirmed
            : RegistrationStatus.Waitlisted;

        if (existing == null)
        {
            _db.EventRegistrations.Add(
                new EventRegistration
                {
                    EventId = eventId,
                    UserId = userId,
                    Status = newStatus,
                    RegisteredAtUtc = now
                });
        }
        else
        {
            // Người đã hủy được đăng ký lại.
            existing.Status = newStatus;
            existing.RegisteredAtUtc = now;
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return newStatus == RegistrationStatus.Confirmed
            ? new(true, "Đăng ký thành công!")
            : new(true, "Sự kiện đã đủ chỗ. Bạn được đưa vào danh sách chờ.");
    }

    public async Task<RegistrationResult> CancelAsync(
        int eventId, string userId)
    {
        await using var transaction =
            await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable);

        var e = await _db.Events
            .FromSqlInterpolated(
                $"SELECT * FROM dbo.Events WITH (UPDLOCK, HOLDLOCK) WHERE Id = {eventId}")
            .SingleOrDefaultAsync();

        if (e == null)
            return new(false, "Không tìm thấy sự kiện");

        if (e.Status != EventStatus.Published ||
            DateTime.UtcNow >= e.RegistrationDeadlineUtc ||
            DateTime.UtcNow >= e.StartAtUtc)
        {
            return new(false, "Đã hết thời gian hủy đăng ký");
        }

        var registration = await _db.EventRegistrations
            .FirstOrDefaultAsync(x =>
                x.EventId == eventId &&
                x.UserId == userId);

        if (registration == null ||
            registration.Status == RegistrationStatus.Cancelled)
        {
            return new(false, "Bạn chưa đăng ký sự kiện này");
        }

        bool wasConfirmed =
            registration.Status == RegistrationStatus.Confirmed;

        registration.Status = RegistrationStatus.Cancelled;

        // Nếu người giữ chỗ hủy, chuyển người đầu
        // danh sách chờ lên danh sách chính.
        if (wasConfirmed)
        {
            var nextPerson = await _db.EventRegistrations
                .Where(x =>
                    x.EventId == eventId &&
                    x.Status == RegistrationStatus.Waitlisted)
                .OrderBy(x => x.RegisteredAtUtc)
                .ThenBy(x => x.Id)
                .FirstOrDefaultAsync();

            if (nextPerson != null)
            {
                nextPerson.Status =
                    RegistrationStatus.Confirmed;
            }
        }

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return new(true, "Hủy đăng ký thành công");
    }
}
