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
    private readonly Label[] baitCoordinateLabels = Enumerable.Range(0, 5)
    .Select(i => new Label
    {
        Text = "Koordinat: Seçilmedi",
        AutoSize = true,
        ForeColor = Color.DimGray,
        Font = new Font("Segoe UI", 9)
    }).ToArray();
   private readonly Label[] baitStatusLabels = Enumerable.Range(0, 5)
    .Select(i => new Label
    {
        Text = "○ SEÇİLMEDİ",
        AutoSize = true,
        ForeColor = Color.Gray,
        Font = new Font("Segoe UI", 8, FontStyle.Bold)
    }).ToArray();
    private readonly Button start = new()
{
    Text = "▶  BAŞLAT",
    Width = 190,
    Height = 55,
    BackColor = Color.FromArgb(25, 160, 95),
    ForeColor = Color.White,
    FlatStyle = FlatStyle.Flat,
    Font = new Font("Segoe UI", 12, FontStyle.Bold),
    Cursor = Cursors.Hand
};

private readonly Button stop = new()
{
    Text = "■  DURDUR",
    Width = 190,
    Height = 55,
    BackColor = Color.FromArgb(190, 45, 55),
    ForeColor = Color.White,
    FlatStyle = FlatStyle.Flat,
    Font = new Font("Segoe UI", 12, FontStyle.Bold),
    Cursor = Cursors.Hand,
    Enabled = false
};
private void StyleActionButtons()
{
    start.FlatAppearance.BorderSize = 2;
    start.FlatAppearance.BorderColor = Color.LimeGreen;
    start.FlatAppearance.MouseOverBackColor = Color.FromArgb(35, 190, 115);

    stop.FlatAppearance.BorderSize = 2;
    stop.FlatAppearance.BorderColor = Color.Crimson;
    stop.FlatAppearance.MouseOverBackColor = Color.FromArgb(220, 65, 75);
}
    private readonly Label baitLabel = new() { Text = "Yem konumu: seçilmedi", AutoSize = true };
    private readonly Label status = new() { Text = "Hazır", AutoSize = true, ForeColor = Color.DarkBlue };
    private readonly Label baitCounterLabel = new()
{
    Text = "Aktif yem: Ana Yem | Kullanım: 0 / 200",
    AutoSize = true,
    ForeColor = Color.DarkGreen
};
    private readonly NumericUpDown timeout = new() { Minimum = 10, Maximum = 180, Value = 45, Width = 70 };
    private readonly NumericUpDown threshold = new() { Minimum = 10, Maximum = 98, Value = 20, Width = 70 };
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
    StyleActionButtons();
        Text = "🎣 METİN2 | FISHING BOT";
        BackColor = Color.FromArgb(13, 20, 35);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new System.Drawing.Size(760, 700);
ForeColor = Color.White;
        LoadBaitSettings();
        baitLabel.Text = "Ana yem: " +
    (baitPoints[0]?.ToString() ?? "Seçilmedi") +
    " | Yedek: " +
    string.Join(", ", baitPoints.Skip(1).Select(p => p?.ToString() ?? "Seçilmedi"));
       Width = 920;Height = 850;
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        var title = new Label { Text = "METİN2 BALIK BOTU", Font = new Font("Segoe UI", 15, FontStyle.Bold), AutoSize = true };
        var subtitle = new Label { Text = "Eski sistem • SPACE ile at / SPACE ile çek", AutoSize = true };
        var timeoutLabel = new Label { Text = "Balık bekleme süresi (sn):", AutoSize = true };
        var thresholdLabel = new Label { Text = "Simge algılama hassasiyeti (%):", AutoSize = true };
        var tabs = new TabControl
{
    Dock = DockStyle.Fill
};
tabs.Font = new Font("Segoe UI", 10, FontStyle.Bold);
tabs.ItemSize = new System.Drawing.Size(145, 42);
tabs.Padding = new Point(18, 7);
tabs.SizeMode = TabSizeMode.Fixed;
tabs.Appearance = TabAppearance.FlatButtons;
tabs.HotTrack = true;
tabs.DrawMode = TabDrawMode.OwnerDrawFixed;

tabs.DrawItem += (sender, e) =>
{
    bool selected = e.Index == tabs.SelectedIndex;

    using var brush = new SolidBrush(
        selected
            ? Color.FromArgb(0, 110, 180)
            : Color.FromArgb(25, 35, 55)
    );
     e.Graphics.FillRectangle(brush, e.Bounds);

    TextRenderer.DrawText(
        e.Graphics,
        tabs.TabPages[e.Index].Text,
        tabs.Font,
        e.Bounds,
        Color.White,
        TextFormatFlags.HorizontalCenter |
        TextFormatFlags.VerticalCenter
    );
};

tabs.SelectedIndexChanged += (sender, e) =>
{
    tabs.Invalidate();
};
var generalTab = new TabPage("🏠 GENEL");
var baitTab = new TabPage("🎣 YEM AYARLARI");
var settingsTab = new TabPage("⚙ AYARLAR");

generalTab.ForeColor = Color.White;
baitTab.ForeColor = Color.White;
settingsTab.ForeColor = Color.White;

tabs.TabPages.Add(generalTab);
tabs.TabPages.Add(baitTab);
tabs.TabPages.Add(settingsTab);
generalTab.BackColor = Color.FromArgb(13, 20, 35);
baitTab.BackColor = Color.FromArgb(13, 20, 35);
settingsTab.BackColor = Color.FromArgb(13, 20, 35);
generalTab.Padding = new Padding(12);
baitTab.Padding = new Padding(12);
settingsTab.Padding = new Padding(12);
       var controls = new FlowLayoutPanel
{
    Dock = DockStyle.Fill,
    BackColor = Color.FromArgb(13, 20, 35),
    FlowDirection = FlowDirection.TopDown,
    Padding = new Padding(16),
    WrapContents = false
};
title.ForeColor = Color.DeepSkyBlue;
title.BackColor = Color.FromArgb(13, 20, 35);
title.Font = new Font("Segoe UI", 24, FontStyle.Bold);
title.Margin = new Padding(5, 10, 5, 8);

subtitle.ForeColor = Color.LightGray;
subtitle.Margin = new Padding(5, 0, 5, 12);
subtitle.Font = new Font("Segoe UI", 11, FontStyle.Regular);
subtitle.ForeColor = Color.Silver;

baitCounterLabel.ForeColor = Color.LightSkyBlue;
baitCounterLabel.Font = new Font("Segoe UI", 11, FontStyle.Bold);
baitCounterLabel.BackColor = Color.FromArgb(20, 65, 100);
baitCounterLabel.Padding = new Padding(8);
baitCounterLabel.BorderStyle = BorderStyle.FixedSingle;
baitCounterLabel.TextAlign = ContentAlignment.MiddleCenter;
baitCounterLabel.MinimumSize = new System.Drawing.Size(220, 38);
baitCounterLabel.Cursor = Cursors.Hand;
baitCounterLabel.AutoSize = true;
baitCounterLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
controls.Controls.Add(title);
controls.Controls.Add(subtitle);
controls.Controls.Add(baitCounterLabel);

var statusPanel = new Panel
{
    Width = 430,
    BorderStyle = BorderStyle.None,
    Height = 55,
    BackColor = Color.FromArgb(25, 35, 55),
    Padding = new Padding(12),
    Margin = new Padding(5, 15, 5, 15)
};

statusPanel.Paint += (sender, e) =>
{
    using var pen = new Pen(Color.DeepSkyBlue, 2);
    e.Graphics.DrawRectangle(
        pen,
        0,
        0,
        statusPanel.Width - 1,
        statusPanel.Height - 1
    );
};
status.Font = new Font("Segoe UI", 13, FontStyle.Bold);
status.ForeColor = Color.DeepSkyBlue;
status.BackColor = Color.FromArgb(25, 35, 55);
status.Location = new Point(12, 15);
status.AutoSize = false;
status.Dock = DockStyle.Fill;
status.TextAlign = ContentAlignment.MiddleCenter;

statusPanel.Controls.Add(status);
controls.Controls.Add(statusPanel);

var buttons = new FlowLayoutPanel
{
    AutoSize = true,
    BackColor = Color.FromArgb(13, 20, 35),
    Padding = new Padding(12),
    Margin = new Padding(5, 10, 5, 10)
};
buttons.FlowDirection = FlowDirection.LeftToRight;
buttons.WrapContents = false;
buttons.Controls.Add(start);
buttons.Controls.Add(stop);
start.Margin = new Padding(8);
stop.Margin = new Padding(8);
buttons.Padding = new Padding(10);
buttons.Margin = new Padding(5, 15, 5, 15);
controls.Controls.Add(buttons);

var hotkeyLabel = new Label
{
    Text = "Durdur: F8 | Acil durdurma: fareyi sol üst köşeye götür",
    Font = new Font("Segoe UI", 9, FontStyle.Bold),
    ForeColor = Color.LightSkyBlue,
    BackColor = Color.FromArgb(25, 35, 55),
    Padding = new Padding(8),
    Margin = new Padding(5, 12, 5, 5),
    AutoSize = true
};

hotkeyLabel.Paint += (sender, e) =>
{
    using var pen = new Pen(Color.DeepSkyBlue, 2);
    e.Graphics.DrawRectangle(
        pen,
        0,
        0,
        hotkeyLabel.Width - 1,
        hotkeyLabel.Height - 1
    );
};

controls.Controls.Add(hotkeyLabel);

generalTab.Controls.Add(controls);
status.Text = "● BOT HAZIR";
status.ForeColor = Color.LightSkyBlue;
statusPanel.BackColor = Color.FromArgb(25, 45, 75);

// YEM AYARLARI SEKME
var baitControls = new FlowLayoutPanel
{
    Dock = DockStyle.Fill,
    FlowDirection = FlowDirection.TopDown,
    Padding = new Padding(16),
    WrapContents = false
};

for (int i = 0; i < chooseBaitButtons.Length; i++)
{
  var baitCard = new Panel
{
    Width = 420,
    Height = 90,
    BackColor = Color.FromArgb(18, 28, 48),
    BorderStyle = BorderStyle.FixedSingle,
    Padding = new Padding(10),
    Margin = new Padding(6)
};

baitCard.Paint += (sender, e) =>
{
    using var pen = new Pen(Color.FromArgb(0, 170, 255), 2);
    e.Graphics.DrawRectangle(
        pen,
        0,
        0,
        baitCard.Width - 1,
        baitCard.Height - 1
    );
};
chooseBaitButtons[i].BackColor = Color.FromArgb(35, 65, 100);
chooseBaitButtons[i].ForeColor = Color.White;
chooseBaitButtons[i].FlatStyle = FlatStyle.Flat;
chooseBaitButtons[i].Font = new Font("Segoe UI Semibold", 9, FontStyle.Bold);
chooseBaitButtons[i].FlatAppearance.BorderColor = Color.DeepSkyBlue;
chooseBaitButtons[i].FlatAppearance.BorderSize = 1;
chooseBaitButtons[i].FlatAppearance.BorderSize = 2;
chooseBaitButtons[i].FlatAppearance.BorderColor = Color.DeepSkyBlue;
chooseBaitButtons[i].FlatAppearance.MouseOverBackColor =
    Color.FromArgb(25, 120, 180);

chooseBaitButtons[i].FlatAppearance.MouseDownBackColor =
    Color.FromArgb(15, 85, 135);
chooseBaitButtons[i].Cursor = Cursors.Hand;
var baitTitle = new Label
{
    Text = i == 0 ? "ANA YEM" : $"YEDEK YEM {i}",
    ForeColor = Color.DeepSkyBlue,
    Font = new Font("Segoe UI", 9, FontStyle.Bold),
    AutoSize = true,
    Location = new Point(10, 5)
};

baitCard.Controls.Add(baitTitle);
chooseBaitButtons[i].Location = new Point(10, 28);
chooseBaitButtons[i].Width = 145;
chooseBaitButtons[i].Height = 38;
    baitCard.Controls.Add(chooseBaitButtons[i]);

    baitCoordinateLabels[i].Location = new Point(165, 10);
    baitCoordinateLabels[i].ForeColor = Color.LightSkyBlue;
    baitCoordinateLabels[i].BackColor = Color.FromArgb(18, 28, 48);
baitCoordinateLabels[i].Font = new Font("Consolas", 9, FontStyle.Bold);
baitCoordinateLabels[i].Font = new Font("Segoe UI", 9, FontStyle.Bold);
    baitCard.Controls.Add(baitCoordinateLabels[i]);
   baitStatusLabels[i].Text = baitPoints[i].HasValue ? "● SEÇİLDİ" : "○ SEÇİLMEDİ";
   baitStatusLabels[i].Font = new Font("Segoe UI", 9, FontStyle.Bold);
baitStatusLabels[i].BackColor = Color.FromArgb(18, 28, 48);
baitStatusLabels[i].ForeColor = baitPoints[i].HasValue ? Color.LightGreen : Color.Gray;
baitStatusLabels[i].Location = new Point(165, 40);

baitCard.Controls.Add(baitStatusLabels[i]);

    baitControls.Controls.Add(baitCard);
}
baitLabel.ForeColor = Color.DeepSkyBlue;
baitLabel.BackColor = Color.FromArgb(18, 28, 48);
baitLabel.Font = new Font("Segoe UI", 9, FontStyle.Bold);
baitLabel.Padding = new Padding(8);
baitLabel.AutoSize = true;
baitControls.Controls.Add(baitLabel);
baitTab.Controls.Add(baitControls);

// AYARLAR SEKME
var settingsControls = new FlowLayoutPanel
{
    Dock = DockStyle.Fill,
    BackColor = Color.FromArgb(13, 20, 35),
    FlowDirection = FlowDirection.TopDown,
    Padding = new Padding(16),
    WrapContents = false
};
timeoutLabel.ForeColor = Color.White;
thresholdLabel.ForeColor = Color.White;

timeoutLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
thresholdLabel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
timeout.BackColor = Color.FromArgb(25, 40, 65);
timeout.ForeColor = Color.White;
timeout.Font = new Font("Segoe UI", 10, FontStyle.Bold);

threshold.BackColor = Color.FromArgb(25, 40, 65);
threshold.ForeColor = Color.White;
threshold.Font = new Font("Segoe UI", 10, FontStyle.Bold);
settingsControls.Controls.Add(timeoutLabel);
settingsControls.Controls.Add(timeout);
settingsControls.Controls.Add(thresholdLabel);
settingsControls.Controls.Add(threshold);

settingsTab.Controls.Add(settingsControls);

Controls.Add(tabs);

        for (int i = 0; i < chooseBaitButtons.Length; i++)
{

    int index = i;

    chooseBaitButtons[i].Click += async (_, _) =>
    {
        status.Text = "3 saniye içinde yem kutusunun ortasına fareyi getir...";
        await Task.Delay(3000);

        baitPoints[index] = Cursor.Position;
        SaveBaitSettings();
        baitCoordinateLabels[index].Text =
    $"Koordinat: X={baitPoints[index]!.Value.X}, Y={baitPoints[index]!.Value.Y}";
    baitStatusLabels[index].Text = "● SEÇİLDİ";
baitStatusLabels[index].ForeColor = Color.LightGreen;

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
        {
            baitPoints[i] = new Point(data[i]![0], data[i]![1]);

            baitCoordinateLabels[i].Text =
                $"Koordinat: X={baitPoints[i]!.Value.X}, Y={baitPoints[i]!.Value.Y}";
        }
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
    status.Text = "● BOT ÇALIŞIYOR";
status.ForeColor = Color.LightGreen;
status.Parent.BackColor = Color.FromArgb(20, 65, 50);

    _ = Task.Run(() => BotLoop(token, waitSeconds, minScore));
}

private void StopBot()
{
    cancellation?.Cancel();
    start.Enabled = true;
    stop.Enabled = false;
    foreach (var button in chooseBaitButtons)
    button.Enabled = true;
    status.Text = "● BOT DURDURULDU";
status.ForeColor = Color.White;
status.Parent.BackColor = Color.FromArgb(100, 30, 40);
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

BeginInvoke(new Action(() => status.Text = "Yem görüntüleri alınıyor..."));
CaptureBaitReferences();
BeginInvoke(new Action(() => status.Text = "Yem görüntüleri alındı, bot başlıyor..."));
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
