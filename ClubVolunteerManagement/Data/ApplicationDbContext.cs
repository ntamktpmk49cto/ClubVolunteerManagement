
using ClubVolunteerManagement.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ClubVolunteerManagement.Data;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Club> Clubs => Set<Club>();

    public DbSet<ClubMember> ClubMembers
        => Set<ClubMember>();

    public DbSet<Event> Events => Set<Event>();

    public DbSet<EventRegistration> EventRegistrations
        => Set<EventRegistration>();

    public DbSet<CheckInSession> CheckInSessions
        => Set<CheckInSession>();

    public DbSet<Attendance> Attendances
        => Set<Attendance>();

    public DbSet<VolunteerHour> VolunteerHours
        => Set<VolunteerHour>();

    public DbSet<Certificate> Certificates
        => Set<Certificate>();

    protected override void OnModelCreating(
        ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // MSSV duy nhất khi được nhập.
        builder.Entity<ApplicationUser>()
            .HasIndex(x => x.StudentCode)
            .IsUnique();

        // Mỗi người chỉ tham gia một lần trong một CLB.
        builder.Entity<ClubMember>()
            .HasIndex(x => new { x.ClubId, x.UserId })
            .IsUnique();

        // Mỗi người chỉ có một bản ghi đăng ký/sự kiện.
        builder.Entity<EventRegistration>()
            .HasIndex(x => new { x.EventId, x.UserId })
            .IsUnique();

        // Hỗ trợ lấy thứ tự danh sách chờ.
        builder.Entity<EventRegistration>()
            .HasIndex(x => new
            {
                x.EventId,
                x.Status,
                x.RegisteredAtUtc
            });

        // Mỗi người điểm danh tối đa một lần/sự kiện.
        builder.Entity<Attendance>()
            .HasIndex(x => new { x.EventId, x.UserId })
            .IsUnique();

        // Mỗi lượt điểm danh chỉ được cộng giờ một lần.
        builder.Entity<VolunteerHour>()
            .HasIndex(x => x.AttendanceId)
            .IsUnique();

        // Mỗi lượt điểm danh chỉ có một chứng nhận.
        builder.Entity<Certificate>()
            .HasIndex(x => x.AttendanceId)
            .IsUnique();

        builder.Entity<Certificate>()
            .HasIndex(x => x.VerificationCode)
            .IsUnique();

        builder.Entity<Event>()
            .Property(x => x.VolunteerHours)
            .HasPrecision(5, 2);

        builder.Entity<VolunteerHour>()
            .Property(x => x.Hours)
            .HasPrecision(5, 2);

        // Liên kết CLB và thành viên.
        builder.Entity<ClubMember>()
            .HasOne(x => x.Club)
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.ClubId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ClubMember>()
            .HasOne(x => x.User)
            .WithMany(x => x.ClubMemberships)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // CLB tổ chức sự kiện.
        builder.Entity<Event>()
            .HasOne(x => x.Club)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.ClubId)
            .OnDelete(DeleteBehavior.Restrict);

        // Đăng ký sự kiện.
        builder.Entity<EventRegistration>()
            .HasOne(x => x.Event)
            .WithMany(x => x.Registrations)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<EventRegistration>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Phiên điểm danh.
        builder.Entity<CheckInSession>()
            .HasOne(x => x.Event)
            .WithMany(x => x.CheckInSessions)
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        // Dữ liệu điểm danh.
        builder.Entity<Attendance>()
            .HasOne(x => x.Event)
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Attendance>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Attendance>()
            .HasOne(x => x.CheckInSession)
            .WithMany()
            .HasForeignKey(x => x.CheckInSessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Giờ tình nguyện.
        builder.Entity<VolunteerHour>()
            .HasOne(x => x.Attendance)
            .WithOne()
            .HasForeignKey<VolunteerHour>(x => x.AttendanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VolunteerHour>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<VolunteerHour>()
            .HasOne(x => x.Event)
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        // Giấy chứng nhận.
        builder.Entity<Certificate>()
            .HasOne(x => x.Attendance)
            .WithOne()
            .HasForeignKey<Certificate>(x => x.AttendanceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Certificate>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Certificate>()
            .HasOne(x => x.Event)
            .WithMany()
            .HasForeignKey(x => x.EventId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}