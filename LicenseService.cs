using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public sealed class LicenseCheckResult
{
    public bool IsValid { get; set; }
    public bool IsLifetime { get; set; }
    public bool IsTemporaryError { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string Message { get; set; } = "";
}

public static class LicenseService
{
    private const string ApiUrl =
        "https://metin2balikbot.runasp.net/api/license/activate";

    private static readonly HttpClient http = new()
    {
        Timeout = TimeSpan.FromSeconds(20)
    };

    public static async Task<LicenseCheckResult> CheckAsync(
        string activationCode,
        string machineId)
    {
        try
        {
            var response = await http.PostAsJsonAsync(ApiUrl, new
            {
                ActivationCode = activationCode,
                MachineId = machineId
            });

            string responseText =
                await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                bool rejected =
                    response.StatusCode == HttpStatusCode.BadRequest ||
                    response.StatusCode == HttpStatusCode.NotFound;

                return new LicenseCheckResult
                {
                    IsValid = false,
                    IsTemporaryError = !rejected,
                    Message = rejected
                        ? responseText
                        : "Sunucu geçici olarak yanıt vermedi."
                };
            }

            using var json = JsonDocument.Parse(responseText);
            var root = json.RootElement;

            bool success =
                root.TryGetProperty("success", out var successElement) &&
                successElement.ValueKind == JsonValueKind.True;

            if (!success)
            {
                return new LicenseCheckResult
                {
                    IsValid = false,
                    IsTemporaryError = true,
                    Message = "Lisans bilgisi doğrulanamadı."
                };
            }

            bool isLifetime =
                root.TryGetProperty("isLifetime", out var lifetimeElement) &&
                lifetimeElement.ValueKind == JsonValueKind.True;

            DateTimeOffset? expiresAt = null;

            if (root.TryGetProperty("expiresAt", out var expiresElement) &&
                expiresElement.ValueKind == JsonValueKind.String)
            {
                if (DateTimeOffset.TryParse(
                    expiresElement.GetString(),
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var parsedDate))
                {
                    expiresAt = parsedDate;
                }
            }

            return new LicenseCheckResult
            {
                IsValid = true,
                IsLifetime = isLifetime,
                ExpiresAt = expiresAt,
                Message = "Lisans geçerli."
            };
        }
        catch (Exception ex) when (
            ex is HttpRequestException ||
            ex is TaskCanceledException ||
            ex is JsonException)
        {
            return new LicenseCheckResult
            {
                IsValid = false,
                IsTemporaryError = true,
                Message = "Sunucu bağlantısı geçici olarak kurulamadı."
            };
        }
        catch
        {
            return new LicenseCheckResult
            {
                IsValid = false,
                IsTemporaryError = true,
                Message = "Lisans kontrolü sırasında beklenmeyen hata oluştu."
            };
        }
    }
}
