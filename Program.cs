using System;
using System.Linq;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = System.Drawing.Point;
namespace Metin2BalikBot;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}

public sealed class MainForm : Form
{
    private readonly Button[] chooseBaitButtons = Enumerable.Range(0, 5)
    .Select(i => new Button
    {
        Text = i == 0 ? "Ana Yem Seç" : $"Yedek Yem {i} Seç",
        Width = 150,
        Height = 34
    }).ToArray();
    private readonly Button start = new() { Text = "BAŞLAT", Width = 130, Height = 38, BackColor = Color.Honeydew };
    private readonly Button stop = new() { Text = "DURDUR", Width = 130, Height = 38, BackColor = Color.MistyRose, Enabled = false };
    private readonly Label baitLabel = new() { Text = "Yem konumu: seçilmedi", AutoSize = true };
    private readonly Label status = new() { Text = "Hazır", AutoSize = true, ForeColor = Color.DarkBlue };
    private readonly Label baitCounterLabel = new()
{
    Text = "Aktif yem: Ana Yem | Kullanım: 0 / 200",
    AutoSize = true,
    ForeColor = Color.DarkGreen
};
    private readonly NumericUpDown timeout = new() { Minimum = 10, Maximum = 180, Value = 45, Width = 70 };
    private readonly NumericUpDown threshold = new() { Minimum = 10, Maximum = 98, Value = 25, Width = 70 };
    private readonly Point?[] baitPoints = new Point?[5];
    private readonly Mat?[] baitReferences = new Mat?[5];
    private readonly string settingsPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    "Metin2BalikBot",
    "bait-settings.json");
    private int currentBaitIndex = 0;
    private int[] baitUseCounts = new int[5];
    private CancellationTokenSource? cancellation;
    private readonly List<Mat> templates = new();

    public MainForm()
    {
        Text = "Metin2 - Eski Sistem Balık Botu";
        LoadBaitSettings();
        baitLabel.Text = "Ana yem: " +
    (baitPoints[0]?.ToString() ?? "Seçilmedi") +
    " | Yedek: " +
    string.Join(", ", baitPoints.Skip(1).Select(p => p?.ToString() ?? "Seçilmedi"));
       Width = 920;Height = 850;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label { Text = "METİN2 BALIK BOTU", Font = new Font("Segoe UI", 15, FontStyle.Bold), AutoSize = true };
        var subtitle = new Label { Text = "Eski sistem • SPACE ile at / SPACE ile çek", AutoSize = true };
        var timeoutLabel = new Label { Text = "Balık bekleme süresi (sn):", AutoSize = true };
        var thresholdLabel = new Label { Text = "Simge algılama hassasiyeti (%):", AutoSize = true };
        var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(16), WrapContents = false };
        controls.Controls.Add(title);
        controls.Controls.Add(subtitle);
        foreach (var button in chooseBaitButtons)
    controls.Controls.Add(button);
        controls.Controls.Add(baitLabel);
        controls.Controls.Add(baitCounterLabel);
        controls.Controls.Add(timeoutLabel);
        controls.Controls.Add(timeout);
        controls.Controls.Add(thresholdLabel);
        controls.Controls.Add(threshold);
        controls.Controls.Add(status);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.Add(start); buttons.Controls.Add(stop);
        controls.Controls.Add(buttons);
        controls.Controls.Add(new Label { Text = "Durdur: F8  |  Acil durdurma: fareyi sol üst köşeye götür", AutoSize = true, ForeColor = Color.DimGray });
        Controls.Add(controls);

        for (int i = 0; i < chooseBaitButtons.Length; i++)
{
    int index = i;

    chooseBaitButtons[i].Click += async (_, _) =>
    {
        status.Text = "3 saniye içinde yem kutusunun ortasına fareyi getir...";
        await Task.Delay(3000);

        baitPoints[index] = Cursor.Position;
        SaveBaitSettings();

        baitLabel.Text = "Ana yem: " +
            (baitPoints[0]?.ToString() ?? "Seçilmedi") +
            " | Yedek: " +
            string.Join(", ", baitPoints.Skip(1).Select(p => p?.ToString() ?? "Seçilmedi"));

        status.Text = $"{chooseBaitButtons[index].Text} konumu kaydedildi";
    };
}
        start.Click += (_, _) => StartBot();
        stop.Click += (_, _) => StopBot();
        KeyPreview = true;
        KeyDown += (_, e) => { if (e.KeyCode == Keys.F8) StopBot(); };
        FormClosing += (_, _) =>
{
    cancellation?.Cancel();

    foreach (var image in templates)
        image.Dispose();

    templates.Clear();
};
    }
    private void SaveBaitSettings()
{
    Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

    var data = baitPoints.Select(p => p.HasValue
        ? new int[] { p.Value.X, p.Value.Y }
        : null).ToArray();

    File.WriteAllText(
        settingsPath,
        System.Text.Json.JsonSerializer.Serialize(data));
}
private void LoadBaitSettings()
{
    if (!File.Exists(settingsPath))
        return;

    try
    {
        var data = System.Text.Json.JsonSerializer
            .Deserialize<int[]?[]>(File.ReadAllText(settingsPath));

        if (data == null || data.Length != baitPoints.Length)
            return;

        for (int i = 0; i < baitPoints.Length; i++)
        {
            if (data[i] != null && data[i]!.Length >= 2)
                baitPoints[i] = new Point(data[i]![0], data[i]![1]);
        }
    }
    catch
    {
        MessageBox.Show("Yem konumları yüklenemedi.");
    }
}
   private void StartBot()
{
   if (!baitPoints.Any(p => p.HasValue))
{
    MessageBox.Show("Önce en az bir yem konumu seç.");
    return;
}

   templates.Clear();

string[] imageNames =
{
    "balik1.png",
    "balik2.png",
    "balik3.png",
    "balik4.png",
    "balik5.png",
    "balik6.png"
};

foreach (string imageName in imageNames)
{
    string imagePath = Path.Combine(AppContext.BaseDirectory, imageName);

    if (!File.Exists(imagePath))
    {
        MessageBox.Show("Balık görseli bulunamadı: " + imageName);
        return;
    }

    Mat image = Cv2.ImRead(imagePath, ImreadModes.Color);

    if (image.Empty())
    {
        image.Dispose();
        MessageBox.Show("Balık görseli okunamadı: " + imageName);
        return;
    }

     templates.Add(image);
}

int waitSeconds = (int)timeout.Value;
currentBaitIndex = Array.FindIndex(baitPoints, p => p.HasValue);
    double minScore = (double)threshold.Value / 100.0;

    cancellation = new CancellationTokenSource();
    var token = cancellation.Token;

    start.Enabled = false;
    stop.Enabled = true;
    foreach (var button in chooseBaitButtons)
    button.Enabled = false;
    status.Text = "Çalışıyor";

    _ = Task.Run(() => BotLoop(token, waitSeconds, minScore));
}

private void StopBot()
{
    cancellation?.Cancel();
    start.Enabled = true;
    stop.Enabled = false;
    foreach (var button in chooseBaitButtons)
    button.Enabled = true;
    status.Text = "Durduruldu";
}
private bool FindBite(double minScore)
{
    using var bmp = new Bitmap(
        Screen.PrimaryScreen!.Bounds.Width,
        Screen.PrimaryScreen.Bounds.Height);
    using (var g = Graphics.FromImage(bmp))
    {
        g.CopyFromScreen(
            Screen.PrimaryScreen.Bounds.Location,
            Point.Empty,
            bmp.Size);
    }
using var rawScreen = BitmapConverter.ToMat(bmp);
var fishArea = new OpenCvSharp.Rect(355, 183, 100, 100);
using var croppedScreen = new Mat(rawScreen, fishArea);
    using var screenGray = new Mat();

   if (croppedScreen.Channels() == 4)
    Cv2.CvtColor(croppedScreen, screenGray, ColorConversionCodes.BGRA2GRAY);
else if (croppedScreen.Channels() == 3)
    Cv2.CvtColor(croppedScreen, screenGray, ColorConversionCodes.BGR2GRAY);
else
    croppedScreen.CopyTo(screenGray);

    double bestScore = 0;
    OpenCvSharp.Point bestLocation = new OpenCvSharp.Point(0, 0);

    foreach (var template in templates)
{
    

    using var templateGray = new Mat();

    if (template.Channels() == 4)
            Cv2.CvtColor(template, templateGray, ColorConversionCodes.BGRA2GRAY);
        else if (template.Channels() == 3)
            Cv2.CvtColor(template, templateGray, ColorConversionCodes.BGR2GRAY);
        else
            template.CopyTo(templateGray);

        if (screenGray.Width < templateGray.Width ||
            screenGray.Height < templateGray.Height)
            continue;

        using var result = new Mat();

        Cv2.MatchTemplate(
            screenGray,
            templateGray,
            result,
            TemplateMatchModes.CCoeffNormed);

        Cv2.MinMaxLoc(
            result,
            out _,
            out double max,
            out _,
            out OpenCvSharp.Point maxLocation);

        if (max > bestScore)
        {
            bestScore = max;
            bestLocation = maxLocation;
        }
    }
if (!IsDisposed && IsHandleCreated)
    BeginInvoke(new Action(() =>
    {
      status.Text = $"Skor: {bestScore * 100:F1}% | X: {bestLocation.X + 295} | Y: {bestLocation.Y + 153}";
    }));
Console.WriteLine($"Skor: {bestScore:F3} | X: {bestLocation.X} | Y: {bestLocation.Y}");
    return bestScore >= minScore;
}
private Mat CaptureBaitSlot(Point slot)
{
    using var bmp = new Bitmap(
        Screen.PrimaryScreen!.Bounds.Width,
        Screen.PrimaryScreen.Bounds.Height);

    using (var g = Graphics.FromImage(bmp))
    {
        g.CopyFromScreen(
            Screen.PrimaryScreen.Bounds.Location,
            Point.Empty,
            bmp.Size);
    }

    int x = slot.X - 10;
    int y = slot.Y - 10;

    using var crop = bmp.Clone(
        new Rectangle(x, y, 20, 20),
        bmp.PixelFormat);

    return BitmapConverter.ToMat(crop);
}
private void CaptureBaitReferences()
{
    for (int i = 0; i < baitPoints.Length; i++)
    {
        if (!baitPoints[i].HasValue)
            continue;

        baitReferences[i]?.Dispose();

        baitReferences[i] = CaptureBaitSlot(
            baitPoints[i]!.Value);
    }
}

private bool IsBaitSlotEmpty(Point slot)
{
    int index = Array.FindIndex(
        baitPoints,
        p => p.HasValue && p.Value == slot);

    if (index < 0 || baitReferences[index] == null)
        return false;

    using var current = CaptureBaitSlot(slot);
    using var diff = new Mat();

    Cv2.Absdiff(baitReferences[index]!, current, diff);

    var mean = Cv2.Mean(diff);

    double difference =
        (mean.Val0 + mean.Val1 + mean.Val2) / 3.0;

    Console.WriteLine(
        $"Yem {index + 1} | Görüntü farkı: {difference:F2}");

    return difference > 12;
}
 private async Task BotLoop(CancellationToken token, int waitSeconds, double minScore)
{
    try
    {
     NativeInput.ActivateGameWindow();
    await Task.Delay(500, token);
    CaptureBaitReferences();
        while (!token.IsCancellationRequested)
        {
            NativeInput.ActivateGameWindow();
            await Task.Delay(300, token);

NativeInput.RightClick(baitPoints[currentBaitIndex]!.Value);
baitUseCounts[currentBaitIndex]++;
BeginInvoke(new Action(() =>
{
    baitCounterLabel.Text =
        $"Aktif yem: {(currentBaitIndex == 0 ? "Ana Yem" : $"Yedek Yem {currentBaitIndex}")} | Kullanım: {baitUseCounts[currentBaitIndex]} / 200";
}));

await Task.Delay(5000, token);
NativeInput.PressSpace();

var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
BeginInvoke(new Action(() => status.Text = $"Balık bekleniyor: {waitSeconds} saniye"));
bool biteFound = false;
int consecutiveDetections = 0;

while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
{
    if (FindBite(minScore))
                {
                    consecutiveDetections++;

                    if (consecutiveDetections >= 1)
                    {
                        await Task.Delay(3000, token);
                        NativeInput.PressSpace();
                        biteFound = true;
                        break;
                    }
                }
                else
                {
                    consecutiveDetections = 0;
                }

                await Task.Delay(150, token);
            }

            await Task.Delay(500, token);
BeginInvoke(new Action(() =>
{
    status.Text = biteFound ? "Balık çekildi - yeni döngü" : "Balık bulunamadı - yeni döngü";
}));
if (baitUseCounts[currentBaitIndex] >= 200)
{
    currentBaitIndex++;

    if (currentBaitIndex >= baitPoints.Length)
    {
        BeginInvoke(new Action(() =>
        {
            MessageBox.Show("Tüm yemler kullanıldı!");
            StopBot();
        }));
        return;
    }
}
        }
    }
    catch (OperationCanceledException) { }
    catch (Exception ex)
    {
        BeginInvoke(new Action(() =>
        {
            status.Text = "HATA: " + ex.Message;
            start.Enabled = true;
            stop.Enabled = false;
            foreach (var button in chooseBaitButtons)
    button.Enabled = true;
        }));
   }
}
}

internal static class NativeInput
{
    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
    private const uint INPUT_MOUSE = 0, INPUT_KEYBOARD = 1, MOUSEEVENTF_RIGHTDOWN = 0x0008, MOUSEEVENTF_RIGHTUP = 0x0010, KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_SPACE = 0x20;
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

[DllImport("user32.dll")]
private static extern bool SetForegroundWindow(IntPtr hWnd);

[DllImport("user32.dll")]
private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

public static bool ActivateGameWindow()
{
    IntPtr hwnd = FindWindow(
        null,
        "Avrasya2 - Gerçek Metin2 Keyfi! www.AvrasyaMt2.com");

    if (hwnd == IntPtr.Zero)
        return false;

    ShowWindow(hwnd, 9);
    return SetForegroundWindow(hwnd);
}

    public static void RightClick(Point p)
{
    Point oldPosition = Cursor.Position;

    int steps = 10;

    for (int i = 1; i <= steps; i++)
    {
        int x = oldPosition.X + (p.X - oldPosition.X) * i / steps;
        int y = oldPosition.Y + (p.Y - oldPosition.Y) * i / steps;

        Cursor.Position = new Point(x, y);
        Thread.Sleep(20);
    }

    var down = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTDOWN } } };
    var up = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTUP } } };

    SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());

    Thread.Sleep(150);
    Cursor.Position = oldPosition;
}
   public static void PressSpace()
{
    var down = new INPUT
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = 0x39,
                dwFlags = 0x0008
            }
        }
    };

    var up = new INPUT
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion
        {
            ki = new KEYBDINPUT
            {
                wVk = 0,
                wScan = 0x39,
                dwFlags = 0x0008 | KEYEVENTF_KEYUP
            }
        }
    };

    SendInput(1, new[] { down }, Marshal.SizeOf<INPUT>());
    Thread.Sleep(80);
     SendInput(1, new[] { up }, Marshal.SizeOf<INPUT>());
}
}
