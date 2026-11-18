using System;
using System.IO;
using System.Security.Cryptography;
using System.Drawing;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

public sealed class LicenseForm : Form
{
    private const string ApiUrl = "https://metin2balikbot.runasp.net/api/license/activate";

    private readonly string licensePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Metin2BalikBot",
        "license.dat");

    private readonly TextBox activationCode = new()
    {
        Width = 280,
        Height = 32,
        Font = new Font("Segoe UI", 11)
    };

    private readonly Button activateButton = new()
    {
        Text = "LİSANSI AKTİF ET",
        Width = 280,
        Height = 42,
        BackColor = Color.FromArgb(35, 150, 85),
        ForeColor = Color.White,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Segoe UI", 10, FontStyle.Bold)
    };

    private readonly Label message = new()
    {
        AutoSize = false,
        Width = 330,
        Height = 55,
        TextAlign = ContentAlignment.MiddleCenter,
        ForeColor = Color.White
    };

    private static readonly HttpClient http = new();

    private void SaveLicense(string code)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(licensePath)!);

        byte[] data = System.Text.Encoding.UTF8.GetBytes(code);

        byte[] encrypted = ProtectedData.Protect(
            data,
            null,
            DataProtectionScope.CurrentUser);

        File.WriteAllBytes(licensePath, encrypted);
    }

    private string LoadLicense()
    {
        byte[] encrypted = File.ReadAllBytes(licensePath);

        byte[] data = ProtectedData.Unprotect(
            encrypted,
            null,
            DataProtectionScope.CurrentUser);

        return System.Text.Encoding.UTF8.GetString(data);
    }

    public LicenseForm()
    {
        Text = "Metin2 Balık Botu - Lisans Aktivasyonu";
        Size = new Size(400, 300);
        MinimumSize = new Size(400, 300);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        BackColor = Color.FromArgb(30, 30, 30);

        var title = new Label
        {
            Text = "LİSANS AKTİVASYONU",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15, FontStyle.Bold),
            AutoSize = false,
            Width = 340,
            Height = 40,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 25)
        };

        var info = new Label
        {
            Text = "Aktivasyon kodunuzu giriniz:",
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(55, 90)
        };

        activationCode.Location = new Point(55, 120);
        activateButton.Location = new Point(55, 170);
        message.Location = new Point(35, 215);

        activateButton.Click += async (_, _) => await ActivateLicense();

        Shown += async (_, _) =>
        {
            if (File.Exists(licensePath))
            {
                try
                {
                    activationCode.Text = LoadLicense();
                    await ActivateLicense();
                }
                catch
                {
                    message.Text = "Kayıtlı lisans okunamadı. Kodunuzu tekrar girin.";
                    message.ForeColor = Color.OrangeRed;
                }
            }
        };

        Controls.Add(title);
        Controls.Add(info);
        Controls.Add(activationCode);
        Controls.Add(activateButton);
        Controls.Add(message);
    }

    private async Task ActivateLicense()
    {
        string code = activationCode.Text.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            message.Text = "Lütfen aktivasyon kodunuzu girin.";
            message.ForeColor = Color.Orange;
            return;
        }

        activateButton.Enabled = false;
        activateButton.Text = "DOĞRULANIYOR...";
        message.Text = "Sunucuya bağlanılıyor...";

        try
        {
            string machineId = GetMachineId();

            var response = await http.PostAsJsonAsync(ApiUrl, new
            {
                ActivationCode = code,
                MachineId = machineId
            });

            string responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                message.Text = responseText;
                message.ForeColor = Color.OrangeRed;
                return;
            }

            using var json = JsonDocument.Parse(responseText);
            var root = json.RootElement;

            if (!root.TryGetProperty("success", out var success) ||
                !success.GetBoolean())
            {
                message.Text = "Lisans doğrulanamadı.";
                message.ForeColor = Color.OrangeRed;
                return;
            }

            SaveLicense(code);

            message.Text = "Lisans doğrulandı! Bot açılıyor...";
            message.ForeColor = Color.LightGreen;

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            message.Text = "Sunucu bağlantı hatası: " + ex.Message;
            message.ForeColor = Color.OrangeRed;
        }
        finally
        {
            if (!IsDisposed && DialogResult != DialogResult.OK)
            {
                activateButton.Enabled = true;
                activateButton.Text = "LİSANSI AKTİF ET";
            }
        }
    }

    private static string GetMachineId()
    {
        using var key = Registry.LocalMachine.OpenSubKey(
            @"SOFTWARE\Microsoft\Cryptography");

        string? machineGuid = key?.GetValue("MachineGuid")?.ToString();

        if (string.IsNullOrWhiteSpace(machineGuid))
            throw new Exception("Bilgisayar kimliği okunamadı.");

        return machineGuid;
    }
}
