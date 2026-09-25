using System.Drawing.Drawing2D;
using Ecosys.Windows.Transport;

ApplicationConfiguration.Initialize();
Application.Run(new EcosysForm());

sealed class EcosysForm : Form
{
    private readonly BluetoothTransport transport = new();
    private readonly Label status = new();
    private readonly Panel statusDot = new();
    private readonly ListBox devices = new();
    private readonly Button scan = new();
    private readonly Button hello = new();

    private static readonly Color Navy = Color.FromArgb(22, 50, 74);
    private static readonly Color Blue = Color.FromArgb(47, 111, 176);
    private static readonly Color Green = Color.FromArgb(45, 143, 92);
    private static readonly Color SoftBlue = Color.FromArgb(234, 242, 247);
    private static readonly Color Page = Color.FromArgb(233, 237, 240);

    public EcosysForm()
    {
        Text = "Ecosys";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(560, 760);
        Size = new Size(620, 900);
        BackColor = Page;
        Font = new Font("Segoe UI", 9.5f);
        DoubleBuffered = true;

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(22), BackColor = Color.White };
        Controls.Add(content);

        var header = new Panel { Location = new Point(0, 0), Size = new Size(576, 64), BackColor = Color.FromArgb(246, 248, 249) };
        content.Controls.Add(header);

        var logo = new EcosysLogo { Location = new Point(14, 13), Size = new Size(38, 38) };
        header.Controls.Add(logo);
        header.Controls.Add(new Label { Text = "Ecosys", AutoSize = true, Font = new Font("Segoe UI", 10.5f, FontStyle.Regular), ForeColor = Color.FromArgb(58, 70, 77), Location = new Point(60, 22) });

        var hero = new Panel { Location = new Point(0, 64), Size = new Size(576, 180), BackColor = Navy };
        hero.Paint += PaintHero;
        content.Controls.Add(hero);
        var heroLogo = new EcosysLogo { Location = new Point(22, 28), Size = new Size(62, 62), DrawRing = true };
        hero.Controls.Add(heroLogo);
        hero.Controls.Add(new Label { Text = "Ecosys", AutoSize = true, Font = new Font("Segoe UI", 17, FontStyle.Regular), ForeColor = Color.FromArgb(234, 242, 247), Location = new Point(96, 38) });
        hero.Controls.Add(new Label { Text = "PRIVATE. DIRECT. YOURS.", AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(169, 214, 196), Location = new Point(98, 66) });

        var bt = new Panel { Location = new Point(22, 112), Size = new Size(532, 42), BackColor = Color.FromArgb(255, 255, 255) };
        bt.BackColor = Color.FromArgb(42, 72, 93);
        hero.Controls.Add(bt);
        statusDot.Size = new Size(8, 8); statusDot.Location = new Point(505, 17); statusDot.BackColor = Color.FromArgb(95, 214, 138);
        bt.Controls.Add(statusDot);
        bt.Controls.Add(new Label { Text = "♢  Bluetooth: bereit", AutoSize = true, ForeColor = Color.FromArgb(234, 242, 247), Location = new Point(14, 12) });
        status.Text = "Bereit";
        status.AutoSize = true;
        status.ForeColor = Color.FromArgb(207, 224, 232);
        status.Location = new Point(330, 12);
        bt.Controls.Add(status);

        AddSection(content, "This device", 0, 266);
        var local = Card(content, 0, 302, 576, 82);
        local.Controls.Add(new Label { Text = Environment.MachineName, AutoSize = true, Font = new Font("Segoe UI Semibold", 11.5f), ForeColor = Navy, Location = new Point(62, 17) });
        local.Controls.Add(new Label { Text = "Dieser PC · Windows", AutoSize = true, ForeColor = Color.FromArgb(91, 107, 116), Location = new Point(62, 44) });
        local.Controls.Add(new Label { Text = "Sichtbar", AutoSize = true, Font = new Font("Segoe UI", 8.5f), ForeColor = Color.FromArgb(33, 122, 78), BackColor = Color.FromArgb(223, 242, 230), Location = new Point(474, 30) });

        AddSection(content, "Nearby devices", 0, 404);
        var refresh = Button("↻", SoftBlue, Navy, 28, 397, 34, 34);
        refresh.Click += async (_, _) => await ScanAsync();
        content.Controls.Add(refresh);

        devices.Location = new Point(0, 440);
        devices.Size = new Size(576, 164);
        devices.BorderStyle = BorderStyle.None;
        devices.BackColor = Color.FromArgb(245, 248, 249);
        devices.ForeColor = Navy;
        devices.Font = new Font("Segoe UI", 9.5f);
        content.Controls.Add(devices);

        scan.Text = "Find nearby devices";
        scan.Location = new Point(0, 616);
        scan.Size = new Size(576, 44);
        StylePrimary(scan);
        scan.Click += async (_, _) => await ScanAsync();
        content.Controls.Add(scan);

        AddSection(content, "Test-Verbindung", 0, 682);
        hello.Text = "Send Hello";
        hello.Location = new Point(0, 718);
        hello.Size = new Size(576, 44);
        StyleGreen(hello);
        hello.Click += async (_, _) =>
        {
            await transport.SendAsync(Ecosys.Windows.Protocol.EcosysMessage.Hello(
                $"windows-{Environment.MachineName}", Environment.MachineName, "windows").ToJsonString());
            SetStatus("→ Hello gesendet");
        };
        content.Controls.Add(hello);

        var footer = new Label { Text = "Weitere Funktionen folgen …", AutoSize = false, TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.FromArgb(147, 161, 168), Location = new Point(0, 774), Size = new Size(576, 36) };
        content.Controls.Add(footer);

        transport.StatusChanged += (_, message) => SetStatus(message);
        transport.MessageReceived += (_, message) => SetStatus($"← {message}");
        Shown += async (_, _) => await ScanAsync();
        FormClosed += async (_, _) => await transport.DisposeAsync();
    }

    private static Panel Card(Control parent, int x, int y, int w, int h)
    {
        var p = new Panel { Location = new Point(x, y), Size = new Size(w, h), BackColor = Color.FromArgb(245, 248, 249) };
        p.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, p.ClientRectangle, Color.FromArgb(227, 233, 235), ButtonBorderStyle.Solid);
        parent.Controls.Add(p);
        var icon = new Panel { Location = new Point(14, 22), Size = new Size(36, 36), BackColor = SoftBlue };
        p.Controls.Add(icon);
        return p;
    }

    private static void AddSection(Control parent, string text, int x, int y)
    {
        parent.Controls.Add(new Label { Text = text.ToUpperInvariant(), AutoSize = true, Font = new Font("Segoe UI Semibold", 8.5f), ForeColor = Navy, Location = new Point(x, y) });
    }

    private static Button Button(string text, Color back, Color fore, int x, int y, int w, int h) =>
        new() { Text = text, Location = new Point(x, y), Size = new Size(w, h), BackColor = back, ForeColor = fore, FlatStyle = FlatStyle.Flat };

    private static void StylePrimary(Button b) { b.BackColor = Blue; b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; }
    private static void StyleGreen(Button b) { b.BackColor = Green; b.ForeColor = Color.White; b.FlatStyle = FlatStyle.Flat; b.FlatAppearance.BorderSize = 0; }

    private async Task ScanAsync()
    {
        scan.Enabled = false;
        devices.Items.Clear();
        SetStatus("Suche nach Ecosys-Geräten …");
        try { await transport.StartAsync(); }
        catch (Exception ex) { SetStatus($"Scan fehlgeschlagen: {ex.Message}"); }
        finally { scan.Enabled = true; }
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(text)); return; }
        status.Text = text;
        devices.Items.Add(text);
        if (devices.Items.Count > 30) devices.Items.RemoveAt(0);
    }

    private sealed class EcosysLogo : Control
    {
        public bool DrawRing { get; set; }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var r = Math.Min(ClientSize.Width, ClientSize.Height);
            var cx = ClientSize.Width / 2f; var cy = ClientSize.Height / 2f;
            if (DrawRing) using (var pen = new Pen(Color.FromArgb(95, 255, 255, 255), 2)) e.Graphics.DrawEllipse(pen, cx-r/2+3, cy-r/2+3, r-6, r-6);
            using var b = new SolidBrush(Blue);
            e.Graphics.FillEllipse(b, cx-r*.32f, cy-r*.32f, r*.64f, r*.64f);
            using var g1 = new SolidBrush(Color.FromArgb(63,174,116));
            using var g2 = new SolidBrush(Green);
            var p1 = new[] { new PointF(cx-r*.20f,cy-r*.10f), new PointF(cx-r*.02f,cy-r*.20f), new PointF(cx+r*.00f,cy-r*.04f), new PointF(cx-r*.15f,cy+r*.02f) };
            var p2 = new[] { new PointF(cx+r*.05f,cy-r*.20f), new PointF(cx+r*.30f,cy-r*.24f), new PointF(cx+r*.28f,cy-r*.03f), new PointF(cx+r*.08f,cy+r*.02f) };
            e.Graphics.FillPolygon(g1, p1); e.Graphics.FillPolygon(g2, p2);
        }
    }

    private void PaintHero(object? sender, PaintEventArgs e)
    {
        using var brush = new LinearGradientBrush(((Control)sender!).ClientRectangle, Color.FromArgb(18,58,94), Color.FromArgb(28,110,79), 45f);
        e.Graphics.FillRectangle(brush, ((Control)sender!).ClientRectangle);
    }
}
