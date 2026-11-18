using System.Diagnostics;
using System.Net.Http.Json;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;

namespace ClubVolunteerManagement.Services;

public class NgrokHostedService : IHostedService
{
    private Process? _ngrokProcess;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NgrokHostedService> _logger;

    // Cổng HTTPS của ứng dụng khi chạy Visual Studio
    private const int AppPort = 7255;

    // Nếu bạn có static domain miễn phí từ ngrok, điền vào đây. Nếu không, để chuỗi rỗng: ""
    private const string CustomDomain = "";

    public NgrokHostedService(IHttpClientFactory httpClientFactory, ILogger<NgrokHostedService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Chạy ngrok ở background thread để không chặn tiến trình khởi động của Web
        _ = Task.Run(async () =>
        {
            try
            {
                StartNgrokProcess();

                // Đợi 2-3 giây cho ngrok khởi tạo xong local API
                await Task.Delay(2500, cancellationToken);

                // Lấy URL công khai do ngrok tạo ra từ API nội bộ của nó (http://127.0.0.1:4040)
                var publicUrl = await GetPublicUrlAsync();

                if (!string.IsNullOrEmpty(publicUrl))
                {
                    _logger.LogInformation(">>> NGROK PUBLIC URL: {Url} <<<", publicUrl);

                    // Tự động bật trình duyệt mặc định mở thẳng link ngrok
                    OpenBrowser(publicUrl);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Không thể tự động kích hoạt ngrok: {Message}", ex.Message);
            }
        }, cancellationToken);

        return Task.CompletedTask;
    }

    private void StartNgrokProcess()
    {
        // Kiểm tra xem đã có tiến trình ngrok nào chạy trước đó chưa, nếu có thì dừng lại
        foreach (var proc in Process.GetProcessesByName("ngrok"))
        {
            try { proc.Kill(); } catch { }
        }

        var domainArg = string.IsNullOrWhiteSpace(CustomDomain) ? "" : $"--domain={CustomDomain} ";
        var arguments = $"http {domainArg}https://localhost:{AppPort}";

        var startInfo = new ProcessStartInfo
        {
            FileName = "ngrok", // Nếu chưa thêm ngrok vào biến môi trường PATH, hãy điền đường dẫn tuyệt đối, ví dụ: @"D:\ngrok.exe"
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        _ngrokProcess = Process.Start(startInfo);
    }

    private async Task<string?> GetPublicUrlAsync()
    {
        var client = _httpClientFactory.CreateClient();
        for (int i = 0; i < 10; i++)
        {
            try
            {
                var response = await client.GetFromJsonAsync<JsonObject>("http://127.0.0.1:4040/api/tunnels");
                var tunnels = response?["tunnels"]?.AsArray();
                var publicUrl = tunnels?.FirstOrDefault()?["public_url"]?.ToString();

                if (!string.IsNullOrEmpty(publicUrl))
                {
                    return publicUrl;
                }
            }
            catch
            {
                // Thử lại nếu ngrok chưa kịp sẵn sàng
                await Task.Delay(1000);
            }
        }
        return null;
    }

    private static void OpenBrowser(string url)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        // Khi bấm Dừng chương trình trong Visual Studio, tự động đóng tiến trình ngrok
        if (_ngrokProcess != null && !_ngrokProcess.HasExited)
        {
            try
            {
                _ngrokProcess.Kill();
                _ngrokProcess.Dispose();
            }
            catch { }
        }

        return Task.CompletedTask;
    }
}