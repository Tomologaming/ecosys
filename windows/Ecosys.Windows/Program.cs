using System.Diagnostics;
using System.Drawing.Drawing2D;
using Windows.Devices.Bluetooth;
using Windows.Devices.Radios;
using Ecosys.Windows.Transport;

ApplicationConfiguration.Initialize();
Application.Run(new EcosysForm());

sealed class EcosysForm : Form
{
    readonly BluetoothTransport transport = new();
    readonly FlowLayoutPanel devicesPanel = new();
    readonly Label statusLabel = new();
    readonly Label deviceCountLabel = new();
    readonly Button scanButton = new();
    readonly Button sendHelloButton = new();
    readonly Label connectionLabel = new();
    readonly Label localNameLabel = new();

    static readonly Color Navy = Color.FromArgb(18, 58, 94);
    static readonly Color Green = Color.FromArgb(45, 143, 92);
    static readonly Color Blue = Color.FromArgb(47, 111, 176);
    static readonly Color Ink = Color.FromArgb(22, 50, 74);
    static readonly Color Muted = Color.FromArgb(103, 117, 125);
    static readonly Color Page = Color.FromArgb(238, 242, 244);
    static readonly Color Card = Color.FromArgb(247, 249, 250);

    public EcosysForm()
    {
        Text = "Ecosys";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(900, 820);
        MinimumSize = new Size(820, 760);
        BackColor = Page;
        DoubleBuffered = true;
        Icon = CreateAppIcon();

        var root = new Panel { Dock = DockStyle.Fill, BackColor = Page, Padding = new Padding(32, 22, 32, 26) };
        Controls.Add(root);

        var top = new Panel { Dock = DockStyle.Top, Height = 38, BackColor = Page };
        top.MouseDown += DragWindow;
        root.Controls.Add(top);

        var title = new Label
        {
            Text = "Ecosys",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10),
            ForeColor = Ink,
            Location = new Point(0, 9)
        };
        top.Controls.Add(title);

        AddTopButton(top, "—", 548, () => WindowState = FormWindowState.Minimized);
        AddTopButton(top, "□", 592, () => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized);
        AddTopButton(top, "×", 636, Close);

        var hero = new GradientPanel { Dock = DockStyle.Top, Height = 190, Padding = new Padding(26) };
        root.Controls.Add(hero);

        var appLogo = new PictureBox { Location = new Point(26, 28), Size = new Size(64, 64), SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Transparent, Image = IconToBitmap(Icon) };
        hero.Controls.Add(appLogo);
        hero.Controls.Add(new Label { Text = "Ecosys", AutoSize = true, Font = new Font("Segoe UI Semibold", 22), ForeColor = Color.White, Location = new Point(106, 25) });
        hero.Controls.Add(new Label { Text = "PRIVATE. DIRECT. YOURS.", AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(185, 222, 205), Location = new Point(108, 61) });

        var btCard = new RoundPanel { Location = new Point(24, 103), Size = new Size(800, 48), Fill = Color.FromArgb(31, 70, 91), Radius = 12 };
        hero.Controls.Add(btCard);
        btCard.Controls.Add(new DotControl { Location = new Point(16, 19), Size = new Size(9, 9), Fill = Color.FromArgb(91, 220, 139) });
        statusLabel.Text = "Bluetooth wird geprüft …";
        statusLabel.AutoSize = true;
        statusLabel.Font = new Font("Segoe UI Semibold", 9);
        statusLabel.ForeColor = Color.White;
        statusLabel.Location = new Point(35, 14);
        btCard.Controls.Add(statusLabel);

        var content = new Panel { Dock = DockStyle.Fill, BackColor = Page, AutoScroll = true, Padding = new Padding(0, 20, 0, 0) };
        root.Controls.Add(content);

        AddSection(content, "DIESER PC", 0, 0);
        var local = MakeCard(content, 0, 28, 632, 74);
        local.Controls.Add(new DeviceGlyph { Location = new Point(14, 16), Size = new Size(38, 38), Kind = "pc" });
        localNameLabel.Text = Environment.MachineName;
        localNameLabel.AutoSize = true;
        localNameLabel.Font = new Font("Segoe UI Semibold", 10);
        localNameLabel.ForeColor = Ink;
        localNameLabel.Location = new Point(66, 14);
        local.Controls.Add(localNameLabel);
        local.Controls.Add(new Label { Text = "Windows · Ecosys Bluetooth", AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Muted, Location = new Point(66, 40) });

        var settings = MakeButton("Bluetooth-Einstellungen", Color.FromArgb(230, 238, 243), Ink, 430, 20, 184, 34);
        settings.Click += (_, _) => OpenSettings("ms-settings:bluetooth");
        local.Controls.Add(settings);

        AddSection(content, "ECOSYS-GERÄTE IN DER NÄHE", 0, 116);
        deviceCountLabel.Text = "Noch nicht gesucht";
        deviceCountLabel.AutoSize = true;
        deviceCountLabel.Font = new Font("Segoe UI", 8.5f);
        deviceCountLabel.ForeColor = Muted;
        deviceCountLabel.Location = new Point(0, 143);
        content.Controls.Add(deviceCountLabel);

        scanButton.Text = "Suchen";
        scanButton.Size = new Size(100, 34);
        scanButton.Location = new Point(730, 134);
        scanButton.Click += async (_, _) => await ScanAsync();
        StyleButton(scanButton, Blue, Color.White);
        content.Controls.Add(scanButton);

        devicesPanel.Location = new Point(0, 180);
        devicesPanel.Size = new Size(832, 230);
        devicesPanel.FlowDirection = FlowDirection.TopDown;
        devicesPanel.WrapContents = false;
        devicesPanel.AutoScroll = true;
        devicesPanel.BackColor = Page;
        devicesPanel.Padding = new Padding(0);
        content.Controls.Add(devicesPanel);

        var hint = new RoundPanel { Location = new Point(0, 422), Size = new Size(832, 62), Fill = Color.FromArgb(232, 239, 243), Radius = 12 };
        content.Controls.Add(hint);
        hint.Controls.Add(new Label
        {
            Text = "Tipp: Beide Geräte müssen Bluetooth aktiviert haben.\nBei der ersten Verbindung kann Windows eine Kopplung bestätigen lassen.",
            AutoSize = false,
            Size = new Size(588, 46),
            Location = new Point(18, 8),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Ink
        });

        AddSection(content, "VERBINDUNG", 0, 508);
        connectionLabel.Text = "Kein Gerät verbunden";
        connectionLabel.AutoSize = true;
        connectionLabel.Font = new Font("Segoe UI Semibold", 10);
        connectionLabel.ForeColor = Ink;
        connectionLabel.Location = new Point(0, 536);
        content.Controls.Add(connectionLabel);

        sendHelloButton.Text = "Hello senden";
        sendHelloButton.Size = new Size(832, 44);
        sendHelloButton.Location = new Point(0, 568);
        sendHelloButton.Click += async (_, _) => await SendHelloAsync();
        StyleButton(sendHelloButton, Green, Color.White);
        sendHelloButton.Enabled = false;
        content.Controls.Add(sendHelloButton);

        var footer = new Label
        {
            Text = "Direkt zwischen deinen Geräten · keine Cloud · keine Server",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(832, 30),
            Location = new Point(0, 630),
            Font = new Font("Segoe UI", 8),
            ForeColor = Color.FromArgb(145, 158, 165)
        };
        content.Controls.Add(footer);

        transport.StatusChanged += (_, message) => SetStatus(message);
        transport.DevicesChanged += (_, devices) => ShowDevices(devices);
        transport.MessageReceived += (_, message) => SetStatus("← " + message);

        Shown += async (_, _) => await ScanAsync();
        FormClosed += async (_, _) => await transport.DisposeAsync();
    }

    async Task ScanAsync()
    {
        scanButton.Enabled = false;
        scanButton.Text = "Suche …";
        try
        {
            var radios = await Radio.GetRadiosAsync();
            var bluetoothRadio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
            if (bluetoothRadio is null || bluetoothRadio.State != RadioState.On)
            {
                SetStatus("Bluetooth ist deaktiviert");
                var result = MessageBox.Show(this,
                    "Ecosys benötigt Bluetooth, um Geräte zu finden. Soll die Windows-Bluetooth-Einstellungsseite geöffnet werden?",
                    "Bluetooth benötigt", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                if (result == DialogResult.Yes)
                    OpenSettings("ms-settings:bluetooth");
                return;
            }

            await transport.ScanAsync();
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus("Bluetooth-Zugriff nicht erlaubt");
            var result = MessageBox.Show(this,
                "Windows hat den Bluetooth-Zugriff für Ecosys nicht freigegeben. Bitte Bluetooth in den Windows-Einstellungen aktivieren bzw. die Geräteberechtigung bestätigen.",
                "Bluetooth-Zugriff", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (result == DialogResult.OK)
                OpenSettings("ms-settings:bluetooth");
        }
        catch (Exception ex)
        {
            SetStatus("Bluetooth-Suche fehlgeschlagen");
            MessageBox.Show(this, ex.Message, "Ecosys Bluetooth", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            scanButton.Enabled = true;
            scanButton.Text = "Suchen";
        }
    }

    void ShowDevices(IReadOnlyList<BluetoothDeviceInfo> devices)
    {
        if (InvokeRequired) { BeginInvoke(() => ShowDevices(devices)); return; }

        devicesPanel.Controls.Clear();
        deviceCountLabel.Text = devices.Count == 0
            ? "Keine Ecosys-Geräte gefunden"
            : $"{devices.Count} Gerät{(devices.Count == 1 ? "" : "e")} gefunden";

        foreach (var device in devices)
        {
            var card = MakeCard(devicesPanel, 0, 0, 608, 68);
            card.Margin = new Padding(0, 0, 0, 8);
            card.Controls.Add(new DeviceGlyph { Location = new Point(14, 15), Size = new Size(38, 38), Kind = "phone" });
            card.Controls.Add(new Label { Text = device.Name, AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f), ForeColor = Ink, Location = new Point(66, 13) });
            card.Controls.Add(new Label { Text = "Ecosys Bluetooth", AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = Muted, Location = new Point(66, 38) });

            var connect = MakeButton("Verbinden", Blue, Color.White, 488, 17, 104, 34);
            connect.Click += async (_, _) => await ConnectAsync(device, connect);
            card.Controls.Add(connect);
            devicesPanel.Controls.Add(card);
        }
    }

    async Task ConnectAsync(BluetoothDeviceInfo device, Button button)
    {
        button.Enabled = false;
        button.Text = "Verbinde …";
        try
        {
            await transport.ConnectAsync(device);
            connectionLabel.Text = $"Verbunden mit {device.Name}";
            sendHelloButton.Enabled = true;
        }
        catch (Exception ex)
        {
            connectionLabel.Text = "Verbindung fehlgeschlagen";
            MessageBox.Show(this, ex.Message, "Ecosys Bluetooth", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            button.Enabled = true;
            button.Text = "Verbinden";
        }
    }

    async Task SendHelloAsync()
    {
        try
        {
            await transport.SendAsync(Ecosys.Windows.Protocol.EcosysMessage
                .Hello($"windows-{Environment.MachineName}", Environment.MachineName, "windows")
                .ToJsonString());
            SetStatus("Hello gesendet");
        }
        catch (Exception ex)
        {
            SetStatus("Senden fehlgeschlagen");
            MessageBox.Show(this, ex.Message, "Ecosys", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void SetStatus(string message)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(message)); return; }
        statusLabel.Text = message.Length > 46 ? message[..46] : message;
    }

    static RoundPanel MakeCard(Control parent, int x, int y, int w, int h)
    {
        var card = new RoundPanel { Location = new Point(x, y), Size = new Size(w, h), Fill = Color.White, Radius = 12 };
        parent.Controls.Add(card);
        return card;
    }

    static void AddSection(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", 8), ForeColor = Ink, Location = new Point(x, y) });
    }

    static Button MakeButton(string text, Color fill, Color fore, int x, int y, int w, int h)
    {
        var b = new Button { Text = text, Location = new Point(x, y), Size = new Size(w, h), FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI Semibold", 8.5f) };
        StyleButton(b, fill, fore);
        return b;
    }

    static void StyleButton(Button b, Color fill, Color fore)
    {
        b.BackColor = fill;
        b.ForeColor = fore;
        b.FlatAppearance.BorderSize = 0;
        b.Cursor = Cursors.Hand;
    }

    static void OpenSettings(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { }
    }

    void AddTopButton(Panel p, string text, int x, Action click)
    {
        var b = new Label { Text = text, AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, Location = new Point(x, 0), Size = new Size(44, 34), Font = new Font("Segoe UI", 9), ForeColor = Muted, Cursor = Cursors.Hand };
        b.Click += (_, _) => click();
        p.Controls.Add(b);
    }

    void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1, 2, 0);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ReleaseCapture();
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wp, int lp);


    static Icon CreateAppIcon() => LoadAppIcon();

    static Icon LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "ecosys.ico");
        return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
    }

    static Bitmap IconToBitmap(Icon? icon) => icon?.ToBitmap() ?? SystemIcons.Application.ToBitmap();


}


sealed class RoundPanel : Panel
{
    public Color Fill { get; set; } = Color.White;
    public int Radius { get; set; } = 12;

    public RoundPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                  ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = RoundedRect(rect, Radius);
        using var brush = new SolidBrush(Fill);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = RoundedRect(rect, Radius);
        using var pen = new Pen(Color.FromArgb(225, 231, 235), 1);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawPath(pen, path);
    }

    static GraphicsPath RoundedRect(Rectangle rect, int radius)
    {
        var path = new GraphicsPath();
        var r = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
        var d = r * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}

sealed class GradientPanel : Panel
{
    public GradientPanel()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                  ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var brush = new LinearGradientBrush(
            rect,
            Color.FromArgb(18, 58, 94),
            Color.FromArgb(28, 110, 79),
            25f);
        e.Graphics.FillRectangle(brush, rect);
    }
}

sealed class DotControl : Control
{
    public Color Fill { get; set; } = Color.White;

    public DotControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                  ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Fill);
        e.Graphics.FillEllipse(brush, rect);
    }
}

sealed class DeviceGlyph : Control
{
    public string Kind { get; set; } = "phone";

    public DeviceGlyph()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint |
                  ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var rect = ClientRectangle;
        if (rect.Width <= 0 || rect.Height <= 0) return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(232, 239, 243));
        e.Graphics.FillEllipse(brush, rect);

        using var pen = new Pen(Color.FromArgb(47, 111, 176), 2);
        if (Kind == "pc")
        {
            var screen = new Rectangle(9, 8, Math.Max(1, rect.Width - 18), Math.Max(1, rect.Height - 17));
            e.Graphics.DrawRoundedRectangle(pen, screen, 4);
            e.Graphics.DrawLine(pen, rect.Width / 2, rect.Height - 9, rect.Width / 2, rect.Height - 5);
            e.Graphics.DrawLine(pen, 12, rect.Height - 5, rect.Width - 12, rect.Height - 5);
        }
        else
        {
            var phone = new Rectangle(13, 7, Math.Max(1, rect.Width - 26), Math.Max(1, rect.Height - 14));
            e.Graphics.DrawRoundedRectangle(pen, phone, 5);
            e.Graphics.FillEllipse(Brushes.White, rect.Width / 2 - 1, 10, 2, 2);
        }
    }
}

static class GraphicsExtensions
{
    public static void DrawRoundedRectangle(this Graphics graphics, Pen pen, Rectangle rect, int radius)
    {
        if (rect.Width <= 0 || rect.Height <= 0) return;
        using var path = new GraphicsPath();
        var r = Math.Max(1, Math.Min(radius, Math.Min(rect.Width, rect.Height) / 2));
        var d = r * 2;
        path.AddArc(rect.X, rect.Y, d, d, 180, 90);
        path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
        path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
        path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        graphics.DrawPath(pen, path);
    }
}
