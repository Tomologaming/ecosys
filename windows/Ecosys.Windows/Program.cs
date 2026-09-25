using System.Diagnostics;
using System.Drawing.Drawing2D;
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
    static readonly Color Text = Color.FromArgb(22, 50, 74);
    static readonly Color Muted = Color.FromArgb(103, 117, 125);
    static readonly Color Page = Color.FromArgb(238, 242, 244);
    static readonly Color Card = Color.FromArgb(247, 249, 250);

    public EcosysForm()
    {
        Text = "Ecosys";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(620, 820);
        MinimumSize = new Size(620, 760);
        BackColor = Page;
        DoubleBuffered = true;

        var root = new Panel { Dock = DockStyle.Fill, BackColor = Page, Padding = new Padding(28) };
        Controls.Add(root);

        var top = new Panel { Dock = DockStyle.Top, Height = 34, BackColor = Page };
        top.MouseDown += DragWindow;
        root.Controls.Add(top);

        var title = new Label
        {
            Text = "Ecosys",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 10),
            ForeColor = Text,
            Location = new Point(0, 7)
        };
        top.Controls.Add(title);

        AddTopButton(top, "—", 468, () => WindowState = FormWindowState.Minimized);
        AddTopButton(top, "□", 512, () => WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized);
        AddTopButton(top, "×", 556, Close);

        var hero = new GradientPanel { Dock = DockStyle.Top, Height = 178, Padding = new Padding(26) };
        root.Controls.Add(hero);

        hero.Controls.Add(new EcosysLogo { Location = new Point(24, 28), Size = new Size(58, 58), DrawRing = true });
        hero.Controls.Add(new Label { Text = "Ecosys", AutoSize = true, Font = new Font("Segoe UI Semibold", 22), ForeColor = Color.White, Location = new Point(98, 25) });
        hero.Controls.Add(new Label { Text = "PRIVATE. DIRECT. YOURS.", AutoSize = true, Font = new Font("Segoe UI", 9), ForeColor = Color.FromArgb(185, 222, 205), Location = new Point(100, 61) });

        var btCard = new RoundPanel { Location = new Point(24, 103), Size = new Size(516, 48), Fill = Color.FromArgb(31, 70, 91), Radius = 12 };
        hero.Controls.Add(btCard);
        btCard.Controls.Add(new DotControl { Location = new Point(16, 19), Size = new Size(9, 9), Fill = Color.FromArgb(91, 220, 139) });
        statusLabel.Text = "Bluetooth wird geprüft …";
        statusLabel.AutoSize = true;
        statusLabel.Font = new Font("Segoe UI Semibold", 9);
        statusLabel.ForeColor = Color.White;
        statusLabel.Location = new Point(35, 14);
        btCard.Controls.Add(statusLabel);

        var content = new Panel { Dock = DockStyle.Fill, BackColor = Page, AutoScroll = true, Padding = new Padding(0, 18, 0, 0) };
        root.Controls.Add(content);

        AddSection(content, "DIESER PC", 0, 0);
        var local = MakeCard(content, 0, 28, 544, 70);
        local.Controls.Add(new DeviceGlyph { Location = new Point(14, 16), Size = new Size(38, 38), Kind = "pc" });
        localNameLabel.Text = Environment.MachineName;
        localNameLabel.AutoSize = true;
        localNameLabel.Font = new Font("Segoe UI Semibold", 10);
        localNameLabel.ForeColor = Text;
        localNameLabel.Location = new Point(66, 14);
        local.Controls.Add(localNameLabel);
        local.Controls.Add(new Label { Text = "Windows · Ecosys Bluetooth", AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Muted, Location = new Point(66, 40) });

        var settings = MakeButton("Bluetooth-Einstellungen", Color.FromArgb(230, 238, 243), Text, 348, 19, 180, 34);
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
        scanButton.Location = new Point(444, 134);
        scanButton.Click += async (_, _) => await ScanAsync();
        StyleButton(scanButton, Blue, Color.White);
        content.Controls.Add(scanButton);

        devicesPanel.Location = new Point(0, 180);
        devicesPanel.Size = new Size(544, 230);
        devicesPanel.FlowDirection = FlowDirection.TopDown;
        devicesPanel.WrapContents = false;
        devicesPanel.AutoScroll = true;
        devicesPanel.BackColor = Page;
        devicesPanel.Padding = new Padding(0);
        content.Controls.Add(devicesPanel);

        var hint = new RoundPanel { Location = new Point(0, 422), Size = new Size(544, 62), Fill = Color.FromArgb(232, 239, 243), Radius = 12 };
        content.Controls.Add(hint);
        hint.Controls.Add(new Label
        {
            Text = "Tipp: Beide Geräte müssen Bluetooth aktiviert haben.\nBei der ersten Verbindung kann Windows eine Kopplung bestätigen lassen.",
            AutoSize = false,
            Size = new Size(500, 46),
            Location = new Point(18, 8),
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Text
        });

        AddSection(content, "VERBINDUNG", 0, 508);
        connectionLabel.Text = "Kein Gerät verbunden";
        connectionLabel.AutoSize = true;
        connectionLabel.Font = new Font("Segoe UI Semibold", 10);
        connectionLabel.ForeColor = Text;
        connectionLabel.Location = new Point(0, 536);
        content.Controls.Add(connectionLabel);

        sendHelloButton.Text = "Hello senden";
        sendHelloButton.Size = new Size(544, 44);
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
            Size = new Size(544, 30),
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
            await transport.ScanAsync();
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
            var card = MakeCard(devicesPanel, 0, 0, 520, 68);
            card.Margin = new Padding(0, 0, 0, 8);
            card.Controls.Add(new DeviceGlyph { Location = new Point(14, 15), Size = new Size(38, 38), Kind = "phone" });
            card.Controls.Add(new Label { Text = device.Name, AutoSize = true, Font = new Font("Segoe UI Semibold", 9.5f), ForeColor = Text, Location = new Point(66, 13) });
            card.Controls.Add(new Label { Text = "Ecosys Bluetooth", AutoSize = true, Font = new Font("Segoe UI", 8), ForeColor = Muted, Location = new Point(66, 38) });

            var connect = MakeButton("Verbinden", Blue, Color.White, 402, 17, 98, 34);
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
        parent.Controls.Add(new Label { Text = text, AutoSize = true, Font = new Font("Segoe UI Semibold", 8), ForeColor = Text, Location = new Point(x, y) });
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

    sealed class GradientPanel : Panel
    {
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var brush = new LinearGradientBrush(ClientRectangle, Color.FromArgb(18, 58, 94), Color.FromArgb(28, 110, 79), 25);
            e.Graphics.FillRectangle(brush, ClientRectangle);
        }
    }

    sealed class RoundPanel : Panel
    {
        public Color Fill { get; set; } = Color.White;
        public int Radius { get; set; } = 12;

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var path = CreatePath();
            using var brush = new SolidBrush(Fill);
            e.Graphics.FillPath(brush, path);
            using var pen = new Pen(Color.FromArgb(226, 232, 235));
            e.Graphics.DrawPath(pen, path);
        }

        GraphicsPath CreatePath()
        {
            var p = new GraphicsPath();
            var d = Radius * 2;
            p.AddArc(0, 0, d, d, 180, 90);
            p.AddArc(Width - d, 0, d, d, 270, 90);
            p.AddArc(Width - d, Height - d, d, d, 0, 90);
            p.AddArc(0, Height - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }
    }

    sealed class DotControl : Control
    {
        public Color Fill { get; set; } = Color.LimeGreen;
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Fill);
            e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
        }
    }

    sealed class EcosysLogo : Control
    {
        public bool DrawRing { get; set; }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = Math.Min(Width, Height);
            var cx = Width / 2f;
            var cy = Height / 2f;

            if (DrawRing)
            {
                using var ring = new Pen(Color.FromArgb(100, 255, 255, 255), 2) { DashPattern = new[] { 1f, 3f } };
                e.Graphics.DrawEllipse(ring, 2, 2, r - 4, r - 4);
            }

            using var blue = new SolidBrush(Blue);
            e.Graphics.FillEllipse(blue, cx - r * .32f, cy - r * .32f, r * .64f, r * .64f);
            using var green = new SolidBrush(Color.FromArgb(63, 174, 116));
            e.Graphics.FillPolygon(green, new[] { new PointF(cx-r*.20f,cy-r*.10f), new PointF(cx-r*.02f,cy-r*.20f), new PointF(cx,cy-r*.04f), new PointF(cx-r*.15f,cy+r*.02f) });
        }
    }

    sealed class DeviceGlyph : Control
    {
        public string Kind { get; set; } = "pc";

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var pen = new Pen(Text, 2);

            if (Kind == "phone")
            {
                e.Graphics.DrawRoundedRectangle(pen, 10, 4, 18, 30, 4);
            }
            else
            {
                e.Graphics.DrawRectangle(pen, 7, 6, 24, 16);
                e.Graphics.DrawLine(pen, 12, 27, 26, 27);
            }
        }
    }
}
