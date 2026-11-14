using System;
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
    private readonly Button chooseBait = new() { Text = "Yem kutusunu seç (3 sn)", Width = 220, Height = 34 };
    private readonly Button start = new() { Text = "BAŞLAT", Width = 130, Height = 38, BackColor = Color.Honeydew };
    private readonly Button stop = new() { Text = "DURDUR", Width = 130, Height = 38, BackColor = Color.MistyRose, Enabled = false };
    private readonly Label baitLabel = new() { Text = "Yem konumu: seçilmedi", AutoSize = true };
    private readonly Label status = new() { Text = "Hazır", AutoSize = true, ForeColor = Color.DarkBlue };
    private readonly NumericUpDown timeout = new() { Minimum = 10, Maximum = 180, Value = 45, Width = 70 };
    private readonly NumericUpDown threshold = new() { Minimum = 60, Maximum = 98, Value = 82, Width = 70 };
    private Point? baitPoint;
    private CancellationTokenSource? cancellation;
    private readonly List<Mat> templates = new();

    public MainForm()
    {
        Text = "Metin2 - Eski Sistem Balık Botu";
        Width = 430; Height = 340;
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
        controls.Controls.Add(chooseBait);
        controls.Controls.Add(baitLabel);
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

        chooseBait.Click += async (_, _) =>
        {
            status.Text = "3 saniye içinde fareyi yem kutusunun ortasına getir...";
            await Task.Delay(3000);
            baitPoint = Cursor.Position;
            baitLabel.Text = $"Yem konumu: X={baitPoint.Value.X}, Y={baitPoint.Value.Y}";
            status.Text = "Yem konumu kaydedildi";
        };
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

   private void StartBot()
{
    if (baitPoint is null)
    {
        MessageBox.Show("Önce yem kutusunu seç.");
        return;
    }

   templates.Clear();

string[] imageNames =
{
    "balik1.png",
    "balik2.png",
    "balik3.png",
    "balik4.png"
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
    double minScore = (double)threshold.Value / 100.0;

    cancellation = new CancellationTokenSource();
    var token = cancellation.Token;

    start.Enabled = false;
    stop.Enabled = true;
    chooseBait.Enabled = false;
    status.Text = "Çalışıyor";

    _ = Task.Run(() => BotLoop(token, waitSeconds, minScore));
}

private void StopBot()
{
    cancellation?.Cancel();
    start.Enabled = true;
    stop.Enabled = false;
    chooseBait.Enabled = true;
    status.Text = "Durduruldu";
}
   private async Task BotLoop(CancellationToken token, int waitSeconds, double minScore)
{
    try
    {
      while (!token.IsCancellationRequested)
{
    NativeInput.ActivateGameWindow();
    await Task.Delay(300, token);

    NativeInput.RightClick(baitPoint!.Value);
            await Task.Delay(350, token);

            NativeInput.PressSpace();

            var deadline = DateTime.UtcNow.AddSeconds(waitSeconds);
            bool biteFound = false;

            while (!token.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
               if (FindBite(minScore))
{
    await Task.Delay(3000, token);

    NativeInput.PressSpace();
    biteFound = true;
    break;
}

               await Task.Delay(50, token);
            }

            await Task.Delay(biteFound ? 1000 : 500, token);
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
            chooseBait.Enabled = true;
        }));
    }
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
    using var screenGray = new Mat();

    if (rawScreen.Channels() == 4)
        Cv2.CvtColor(rawScreen, screenGray, ColorConversionCodes.BGRA2GRAY);
    else if (rawScreen.Channels() == 3)
        Cv2.CvtColor(rawScreen, screenGray, ColorConversionCodes.BGR2GRAY);
    else
        rawScreen.CopyTo(screenGray);

    int centerX = screenGray.Width / 2;
    int centerY = screenGray.Height / 2;

    int regionX = 0;
int regionY = 0;
int regionWidth = screenGray.Width;
int regionHeight = screenGray.Height;

    using var searchArea = new Mat(
        screenGray,
        new Rect(regionX, regionY, regionWidth, regionHeight));

    double bestScore = 0;

    foreach (var template in templates)
    {
        using var templateGray = new Mat();

        if (template.Channels() == 4)
            Cv2.CvtColor(template, templateGray, ColorConversionCodes.BGRA2GRAY);
        else if (template.Channels() == 3)
            Cv2.CvtColor(template, templateGray, ColorConversionCodes.BGR2GRAY);
        else
            template.CopyTo(templateGray);

        if (searchArea.Width < templateGray.Width ||
            searchArea.Height < templateGray.Height)
            continue;

        using var result = new Mat();

        Cv2.MatchTemplate(
            searchArea,
            templateGray,
            result,
            TemplateMatchModes.CCoeffNormed);

        Cv2.MinMaxLoc(result, out _, out double max, out _, out _);

        if (max > bestScore)
            bestScore = max;
    }

    BeginInvoke(new Action(() =>
    {
        status.Text = "En yüksek algılama skoru: " +
            (bestScore * 100).ToString("F1") + "%";
    }));

    return bestScore >= minScore;
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
        Cursor.Position = p;
        var down = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTDOWN } } };
        var up = new INPUT { type = INPUT_MOUSE, U = new InputUnion { mi = new MOUSEINPUT { dwFlags = MOUSEEVENTF_RIGHTUP } } };
        SendInput(2, new[] { down, up }, Marshal.SizeOf<INPUT>());
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
