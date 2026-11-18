
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ClubVolunteerManagement.Services;

// Dữ liệu đầu vào để tạo giấy chứng nhận.
public class CertificatePdfData
{
    public string FullName { get; set; } = string.Empty;

    public string StudentCode { get; set; } = string.Empty;

    public string EventTitle { get; set; } = string.Empty;

    public string ClubName { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public decimal Hours { get; set; }

    public DateTime EventStartAtLocal { get; set; }

    public DateTime EventEndAtLocal { get; set; }

    public DateTime IssuedAtLocal { get; set; }

    public string VerificationCode { get; set; } = string.Empty;

    public string VerificationUrl { get; set; } = string.Empty;
}

public class CertificatePdfService
{
    // Tạo giấy chứng nhận PDF.
    public byte[] Generate(CertificatePdfData model)
    {
        if (string.IsNullOrWhiteSpace(model.VerificationUrl))
            throw new ArgumentException("Thiếu đường dẫn xác thực.");

        // 1. Tạo mã QR chứa đường dẫn xác thực.
        using var qrGenerator = new QRCodeGenerator();

        using var qrData = qrGenerator.CreateQrCode(
            model.VerificationUrl,
            QRCodeGenerator.ECCLevel.Q);

        var qrBytes = new PngByteQRCode(qrData)
            .GetGraphic(10);

        // 2. Tạo tài liệu PDF.
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);

                page.DefaultTextStyle(
                    style => style.FontSize(11));

                // Nội dung chính.
                page.Content()
                    .Border(3)
                    .BorderColor(Colors.Blue.Darken2)
                    .Padding(28)
                    .Column(column =>
                    {
                        column.Spacing(13);

                        // Tên hệ thống.
                        column.Item()
                            .AlignCenter()
                            .Text(
                                "HỆ THỐNG QUẢN LÝ CLB VÀ HOẠT ĐỘNG TÌNH NGUYỆN")
                            .FontSize(11)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        // Tiêu đề chứng nhận.
                        column.Item()
                            .PaddingTop(12)
                            .AlignCenter()
                            .Text("GIẤY CHỨNG NHẬN")
                            .FontSize(27)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        column.Item()
                            .AlignCenter()
                            .Text("THAM GIA HOẠT ĐỘNG TÌNH NGUYỆN")
                            .FontSize(13)
                            .Bold();

                        column.Item()
                            .PaddingVertical(5)
                            .LineHorizontal(1)
                            .LineColor(Colors.Blue.Medium);

                        // Người nhận chứng nhận.
                        column.Item()
                            .AlignCenter()
                            .Text("Chứng nhận rằng")
                            .FontSize(13);

                        column.Item()
                            .AlignCenter()
                            .Text(model.FullName)
                            .FontSize(23)
                            .Bold()
                            .FontColor(Colors.Blue.Darken2);

                        column.Item()
                            .AlignCenter()
                            .Text($"Mã số sinh viên: {model.StudentCode}");

                        column.Item()
                            .PaddingTop(5)
                            .AlignCenter()
                            .Text("Đã tham gia hoạt động tình nguyện:");

                        // Tên sự kiện.
                        column.Item()
                            .AlignCenter()
                            .Text(model.EventTitle)
                            .FontSize(18)
                            .Bold();

                        column.Item()
                            .AlignCenter()
                            .Text($"Đơn vị tổ chức: {model.ClubName}");

                        column.Item()
                            .AlignCenter()
                            .Text($"Địa điểm: {model.Location}");

                        column.Item()
                            .AlignCenter()
                            .Text(
                                $"Thời gian: " +
                                $"{model.EventStartAtLocal:dd/MM/yyyy HH:mm} - " +
                                $"{model.EventEndAtLocal:dd/MM/yyyy HH:mm}");

                        // Số giờ tình nguyện.
                        column.Item()
                            .PaddingTop(10)
                            .AlignCenter()
                            .Border(1)
                            .BorderColor(Colors.Green.Medium)
                            .Padding(12)
                            .Text(
                                $"SỐ GIỜ TÌNH NGUYỆN ĐƯỢC CÔNG NHẬN: " +
                                $"{model.Hours:0.##} GIỜ")
                            .FontSize(14)
                            .Bold()
                            .FontColor(Colors.Green.Darken2);

                        // Ngày cấp và mã QR.
                        column.Item()
                            .PaddingTop(12)
                            .Row(row =>
                            {
                                row.RelativeItem()
                                    .Column(details =>
                                    {
                                        details.Item()
                                            .Text(
                                                $"Ngày cấp: " +
                                                $"{model.IssuedAtLocal:dd/MM/yyyy}");

                                        details.Item()
                                            .PaddingTop(12)
                                            .Text("Mã xác thực:")
                                            .Bold();

                                        details.Item()
                                            .Text(model.VerificationCode)
                                            .FontSize(8);

                                        details.Item()
                                            .PaddingTop(8)
                                            .Text(
                                                "Quét mã QR để kiểm tra " +
                                                "tính hợp lệ của chứng nhận.")
                                            .FontSize(9);
                                    });

                                row.ConstantItem(125)
                                    .Column(qrColumn =>
                                    {
                                        qrColumn.Item()
                                            .Image(qrBytes)
                                            .FitArea();

                                        qrColumn.Item()
                                            .AlignCenter()
                                            .Text("MÃ QR XÁC THỰC")
                                            .FontSize(8)
                                            .Bold();
                                    });
                            });
                    });

                // Chân trang.
                page.Footer()
                    .PaddingTop(10)
                    .AlignCenter()
                    .Text(
                        "Chứng nhận được tạo bởi hệ thống " +
                        "quản lý CLB và hoạt động tình nguyện.")
                    .FontSize(8)
                    .FontColor(Colors.Grey.Darken1);
            });
        });

        // 3. Xuất PDF thành byte[].
        return document.GeneratePdf();
    }
}
