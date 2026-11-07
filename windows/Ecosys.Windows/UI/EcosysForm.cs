using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using Ecosys.Windows.Protocol;
using Ecosys.Windows.Security;
using Ecosys.Windows.Transport;

namespace Ecosys.Windows.UI;

internal sealed class EcosysForm : Form
{
    private const int DefaultCardWidth = 560;
    private const int TitleBarHeight = 46;
    private const int HeroHeight = 190;

    private readonly BluetoothTransport transport = new();
    private readonly CardStack content = new();
    private readonly Panel titleBar = new();
    private readonly Panel hero = new GradientHeader();
    private readonly Panel statusRule = new();
    private readonly Label statusLabel = new();
    private readonly Dot statusDot = new();
    private readonly Label countLabel = new();
    private readonly Label connectionTitle = new();
    private readonly Label connectionDetail = new();
    private readonly Label activityLabel = new();
    private readonly DeviceListPanel deviceList = new();
    private readonly RoundedButton scanButton = new();
    private readonly RoundedButton sendButton = new();
    private readonly RoundedButton disconnectButton = new();
    private readonly List<string> activity = new();
    private readonly Label localIdLabel = new();

    private DeviceIdentity? identity;
    private string localSender = "windows";

    public EcosysForm()
    {
        Text = "Ecosys";
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(700, 840);
        MinimumSize = new Size(560, 620);
        BackColor = Theme.Page;
        DoubleBuffered = true;
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = LogoArt.AppIcon;
        KeyPreview = true;

        BuildUi();
        WireEvents();

        AddLog("Ecosys gestartet");
        Shown += async (_, _) =>
        {
            UpdateMaximizedBounds();
            LayoutChrome();
            await ScanAsync();
        };
        Resize += (_, _) => LayoutChrome();
        FormClosed += async (_, _) =>
        {
            identity?.Dispose();
            await transport.DisposeAsync();
        };
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            BackColor = Theme.Page
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, TitleBarHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, HeroHeight));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        Controls.Add(root);

        BuildTitleBar();
        BuildHero();
        root.Controls.Add(titleBar, 0, 0);
        root.Controls.Add(hero, 0, 1);
        root.Controls.Add(content, 0, 2);

        content.Dock = DockStyle.Fill;
        content.Padding = new Padding(26, 16, 26, 24);
        BuildContent();
        content.Resize += (_, _) => content.Relayout();
    }

    private void BuildTitleBar()
    {
        titleBar.Dock = DockStyle.Fill;
        titleBar.BackColor = Theme.Page;

        var mark = new PictureBox
        {
            Image = LogoArt.Image,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(22, 22),
            Location = new Point(24, 12),
            BackColor = Theme.Page
        };
        var wordmark = new Label
        {
            Text = "Ecosys",
            Font = Theme.Semibold,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(54, 13),
            BackColor = Theme.Page
        };
        var border = new Panel { Dock = DockStyle.Bottom, Height = 1, BackColor = Theme.Line };

        titleBar.Controls.Add(mark);
        titleBar.Controls.Add(wordmark);
        titleBar.Controls.Add(border);

        mark.MouseDown += DragWindow;
        mark.MouseDoubleClick += (_, _) => ToggleMaximize();
        wordmark.MouseDown += DragWindow;
        wordmark.MouseDoubleClick += (_, _) => ToggleMaximize();
        titleBar.MouseDown += DragWindow;
        titleBar.MouseDoubleClick += (_, _) => ToggleMaximize();

        titleBar.Controls.Add(ChromeButton("—", false, () => WindowState = FormWindowState.Minimized));
        titleBar.Controls.Add(ChromeButton("▢", false, ToggleMaximize));
        titleBar.Controls.Add(ChromeButton("✕", true, Close));
    }

    private Button ChromeButton(string text, bool close, Action action)
    {
        var button = new RoundedButton
        {
            Text = text,
            Size = new Size(42, 30),
            Radius = 8,
            FillColor = Theme.Page,
            HoverColor = close ? Color.FromArgb(232, 74, 78) : Color.FromArgb(226, 232, 236),
            PressedColor = close ? Color.FromArgb(208, 58, 62) : Color.FromArgb(214, 222, 227),
            TextColor = close ? Theme.Muted : Theme.Muted,
            HoverTextColor = close ? Color.White : Theme.Ink,
            Font = new Font("Segoe UI", 9.5f),
            BackColor = Theme.Page,
            TabStop = false
        };
        button.Click += (_, _) => action();
        return button;
    }

    private void BuildHero()
    {
        hero.Dock = DockStyle.Fill;

        var logo = new PictureBox
        {
            Image = LogoArt.Image,
            SizeMode = PictureBoxSizeMode.Zoom,
            Size = new Size(64, 64),
            Location = new Point(26, 24),
            BackColor = Color.Transparent
        };
        var title = new Label
        {
            Text = "Ecosys",
            Font = Theme.Hero,
            ForeColor = Color.White,
            AutoSize = true,
            Location = new Point(104, 22),
            BackColor = Color.Transparent
        };
        var tagline = new Label
        {
            Text = "P R I V A T .   D I R E K T .   D E I N S .",
            Font = Theme.Section,
            ForeColor = Color.FromArgb(158, 214, 195),
            AutoSize = true,
            Location = new Point(106, 66),
            BackColor = Color.Transparent
        };
        var subtitle = new Label
        {
            Text = "Geräte verbinden sich direkt – ohne Cloud, ohne Konto.",
            Font = Theme.HeroSub,
            ForeColor = Color.FromArgb(206, 222, 228),
            AutoSize = true,
            Location = new Point(106, 88),
            BackColor = Color.Transparent
        };

        statusRule.BackColor = Color.FromArgb(52, 84, 96);
        statusRule.Size = new Size(DefaultCardWidth, 1);
        statusRule.Location = new Point(26, 134);
        statusRule.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        statusDot.Size = new Size(9, 9);
        statusDot.Fill = Color.FromArgb(132, 150, 160);
        statusDot.BackColor = Color.Transparent;
        statusDot.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;

        statusLabel.Text = "Bluetooth wird geprüft …";
        statusLabel.Font = Theme.Semibold;
        statusLabel.ForeColor = Color.FromArgb(214, 228, 233);
        statusLabel.AutoSize = false;
        statusLabel.AutoEllipsis = true;
        statusLabel.BackColor = Color.Transparent;
        statusLabel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        hero.Controls.Add(logo);
        hero.Controls.Add(title);
        hero.Controls.Add(tagline);
        hero.Controls.Add(subtitle);
        hero.Controls.Add(statusRule);
        hero.Controls.Add(statusDot);
        hero.Controls.Add(statusLabel);
    }

    private void BuildContent()
    {
        var identityCard = BuildIdentityCard();
        content.AddItem(identityCard, 16);

        content.AddItem(BuildNearbyHeader(), 6);

        deviceList.EmptyTitle = "Noch nicht gesucht";
        deviceList.EmptyDetail = "Tippe auf „Suchen“, um Ecosys-Geräte in der Nähe zu finden.";
        content.AddItem(deviceList, 12);

        var hint = BuildHintCard();
        content.AddItem(hint, 20);

        content.AddItem(Section("VERBINDUNG"), 6);
        content.AddItem(BuildConnectionCard(), 18);

        content.AddItem(Section("AKTIVITÄT"), 6);
        activityLabel.Font = Theme.Mono;
        activityLabel.ForeColor = Theme.Muted;
        activityLabel.BackColor = Color.Transparent;
        activityLabel.Dock = DockStyle.Fill;
        activityLabel.TextAlign = ContentAlignment.TopLeft;
        activityLabel.AutoSize = false;

        var activityCard = CreateCard(112);
        activityCard.Padding = new Padding(16, 12, 16, 12);
        activityCard.Controls.Add(activityLabel);
        content.AddItem(activityCard, 12);

        var footer = new Label
        {
            Text = "Direkt zwischen deinen Geräten · keine Cloud · keine Server",
            Font = Theme.BodyTiny,
            ForeColor = Theme.Faint,
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = false,
            Height = 26,
            BackColor = Theme.Page
        };
        content.AddItem(footer, 12);

        RefreshActivity();
    }

    private RoundedPanel BuildIdentityCard()
    {
        var card = CreateCard(80);
        card.Controls.Add(new IconTile(40, 12) { Location = new Point(16, 20) });

        var name = new Label
        {
            Text = Environment.MachineName,
            Font = Theme.CardTitle,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(68, 18),
            BackColor = Color.Transparent
        };
        var platform = new Label
        {
            Text = "Windows · Ecosys Bluetooth",
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(68, 40),
            BackColor = Color.Transparent
        };

        var idCaption = new Label
        {
            Text = "GERÄTE-ID",
            Font = Theme.Section,
            ForeColor = Theme.Faint,
            AutoSize = true,
            Location = new Point(236, 18),
            BackColor = Color.Transparent
        };
        localIdLabel.Text = "wird geladen";
        localIdLabel.Font = Theme.Mono;
        localIdLabel.ForeColor = Theme.Muted;
        localIdLabel.AutoSize = true;
        localIdLabel.Location = new Point(236, 36);
        localIdLabel.BackColor = Color.Transparent;

        var settings = new RoundedButton
        {
            Text = "Bluetooth-Einstellungen",
            Size = new Size(176, 34),
            Location = new Point(DefaultCardWidth - 176 - 16, 23),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FillColor = Color.FromArgb(232, 238, 242),
            HoverColor = Color.FromArgb(219, 229, 235),
            PressedColor = Color.FromArgb(208, 219, 226),
            TextColor = Theme.Ink,
            HoverTextColor = Theme.Ink,
            Radius = 9,
            Font = Theme.Semibold
        };
        settings.Click += (_, _) => OpenSettings("ms-settings:bluetooth");

        card.Controls.Add(name);
        card.Controls.Add(platform);
        card.Controls.Add(idCaption);
        card.Controls.Add(localIdLabel);
        card.Controls.Add(settings);

        try
        {
            identity = new DeviceIdentity();
            var deviceId = identity.DeviceId;
            localIdLabel.Text = deviceId.Length > 14
                ? $"{deviceId[..10]}…{deviceId[^4..]}"
                : deviceId;
            localSender = $"windows-{deviceId}";
        }
        catch (Exception ex)
        {
            localIdLabel.Text = "nicht verfügbar";
            AddLog($"Geräteidentität nicht verfügbar: {ex.Message}");
        }

        return card;
    }

    private Control BuildNearbyHeader()
    {
        var host = new Panel { Height = 46, Width = DefaultCardWidth, BackColor = Theme.Page };

        var title = new Label
        {
            Text = "ECOSYS-GERÄTE IN DER NÄHE",
            Font = Theme.Section,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(2, 2),
            BackColor = Theme.Page
        };

        countLabel.Text = "Noch nicht gesucht";
        countLabel.Font = Theme.BodySmall;
        countLabel.ForeColor = Theme.Muted;
        countLabel.AutoSize = true;
        countLabel.Location = new Point(2, 21);
        countLabel.BackColor = Theme.Page;

        scanButton.Text = "Suchen";
        scanButton.Size = new Size(104, 32);
        scanButton.FillColor = Theme.Blue;
        scanButton.HoverColor = Theme.BlueHover;
        scanButton.PressedColor = Theme.BlueHover;
        scanButton.TextColor = Color.White;
        scanButton.Font = Theme.Semibold;
        scanButton.BackColor = Theme.Page;
        scanButton.Radius = 9;
        scanButton.Location = new Point(DefaultCardWidth - 104, 3);
        scanButton.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        scanButton.Click += async (_, _) => await ScanAsync();

        host.Controls.Add(title);
        host.Controls.Add(countLabel);
        host.Controls.Add(scanButton);
        host.Resize += (_, _) => scanButton.Location = new Point(host.Width - scanButton.Width, 3);
        return host;
    }

    private static Panel Section(string text, Control? right = null)
    {
        var host = new Panel { Height = right is null ? 24 : 32, Width = DefaultCardWidth, BackColor = Theme.Page };
        var label = new Label
        {
            Text = text,
            Font = Theme.Section,
            ForeColor = Theme.Ink,
            AutoSize = true,
            BackColor = Theme.Page
        };
        label.Location = new Point(2, right is null ? 6 : 10);
        host.Controls.Add(label);

        if (right is not null)
        {
            right.Dock = DockStyle.None;
            right.Location = new Point(DefaultCardWidth - right.Width, 0);
            right.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            host.Controls.Add(right);
        }

        return host;
    }

    private RoundedPanel BuildHintCard()
    {
        var card = new RoundedPanel
        {
            Height = 62,
            Fill = Theme.MintTint,
            BorderColor = Theme.MintLine,
            Radius = 12
        };
        var title = new Label
        {
            Text = "Beide Geräte brauchen Bluetooth",
            Font = Theme.Semibold,
            ForeColor = Color.FromArgb(32, 78, 62),
            AutoSize = true,
            Location = new Point(16, 10),
            BackColor = Color.Transparent
        };
        var detail = new Label
        {
            Text = "Beim ersten Verbinden kann Windows eine Kopplung bestätigen lassen.",
            Font = Theme.BodySmall,
            ForeColor = Color.FromArgb(72, 108, 92),
            AutoSize = true,
            Location = new Point(16, 32),
            BackColor = Color.Transparent
        };
        card.Controls.Add(title);
        card.Controls.Add(detail);
        return card;
    }

    private RoundedPanel BuildConnectionCard()
    {
        var card = CreateCard(112);

        connectionTitle.Text = "Kein Gerät verbunden";
        connectionTitle.Font = Theme.CardTitle;
        connectionTitle.ForeColor = Theme.Ink;
        connectionTitle.AutoSize = true;
        connectionTitle.Location = new Point(16, 14);
        connectionTitle.BackColor = Color.Transparent;

        connectionDetail.Text = "Wähle oben ein Gerät aus, um eine Verbindung aufzubauen.";
        connectionDetail.Font = Theme.BodySmall;
        connectionDetail.ForeColor = Theme.Muted;
        connectionDetail.AutoSize = true;
        connectionDetail.Location = new Point(16, 38);
        connectionDetail.BackColor = Color.Transparent;

        sendButton.Text = "Hello senden";
        sendButton.Size = new Size(132, 36);
        sendButton.Location = new Point(16, 62);
        sendButton.FillColor = Theme.Green;
        sendButton.HoverColor = Theme.GreenHover;
        sendButton.PressedColor = Theme.GreenHover;
        sendButton.TextColor = Color.White;
        sendButton.Radius = 9;
        sendButton.Enabled = false;
        sendButton.Click += async (_, _) => await SendHelloAsync();

        disconnectButton.Text = "Trennen";
        disconnectButton.Size = new Size(96, 36);
        disconnectButton.Location = new Point(156, 62);
        disconnectButton.FillColor = Theme.Card;
        disconnectButton.HoverColor = Color.FromArgb(246, 248, 249);
        disconnectButton.PressedColor = Color.FromArgb(238, 242, 244);
        disconnectButton.TextColor = Theme.Muted;
        disconnectButton.HoverTextColor = Theme.Ink;
        disconnectButton.OutlineColor = Theme.Line;
        disconnectButton.Radius = 9;
        disconnectButton.Enabled = false;
        disconnectButton.Click += async (_, _) => await DisconnectAsync();

        card.Controls.Add(connectionTitle);
        card.Controls.Add(connectionDetail);
        card.Controls.Add(sendButton);
        card.Controls.Add(disconnectButton);
        return card;
    }

    private static RoundedPanel CreateCard(int height) => new()
    {
        Height = height,
        Width = DefaultCardWidth,
        Radius = 14
    };

    private void WireEvents()
    {
        transport.StatusChanged += (_, message) => SetStatus(message);
        transport.DevicesChanged += (_, devices) => ShowDevices(devices);
        transport.MessageReceived += (_, message) => OnMessage(message);

        deviceList.ConnectRequested += async (_, device) => await ConnectAsync(device);
    }

    private async Task ScanAsync()
    {
        scanButton.Enabled = false;
        scanButton.Text = "Suche …";
        try
        {
            AddLog("Suche nach Ecosys-Geräten");
            await transport.ScanAsync();
        }
        catch (Exception ex)
        {
            SetStatus("Bluetooth-Suche fehlgeschlagen");
            AddLog($"Suche fehlgeschlagen: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Ecosys Bluetooth",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            scanButton.Enabled = true;
            scanButton.Text = "Suchen";
        }
    }

    private void ShowDevices(IReadOnlyList<BluetoothDeviceInfo> devices)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => ShowDevices(devices));
            return;
        }

        deviceList.EmptyTitle = devices.Count == 0
            ? "Keine Ecosys-Geräte gefunden"
            : "Noch nicht gesucht";
        deviceList.EmptyDetail = "Aktiviere Bluetooth auf dem anderen Gerät und öffne dort Ecosys.";
        deviceList.SetDevices(devices);
        content.Relayout();
        countLabel.Text = devices.Count == 0
            ? "Keine Ecosys-Geräte gefunden"
            : $"{devices.Count} Gerät{(devices.Count == 1 ? "" : "e")} gefunden";
    }

    private async Task ConnectAsync(BluetoothDeviceInfo device)
    {
        SetConnection(device.Name, "Verbindung wird aufgebaut …");
        try
        {
            AddLog($"Verbinde mit {device.Name}");
            await transport.ConnectAsync(device);
            SetConnection(device.Name, $"Verbunden über {device.Detail}");
            AddLog($"Verbunden mit {device.Name}");
        }
        catch (Exception ex)
        {
            SetConnection(null, "Verbindung fehlgeschlagen");
            AddLog($"Verbindung fehlgeschlagen: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Ecosys Bluetooth",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task SendHelloAsync()
    {
        try
        {
            var message = EcosysMessage.Hello(localSender, Environment.MachineName, "windows");
            await transport.SendAsync(message.ToJsonString());
            SetStatus("Hello gesendet");
            AddLog("→ hello gesendet");
        }
        catch (Exception ex)
        {
            SetStatus("Senden fehlgeschlagen");
            AddLog($"Senden fehlgeschlagen: {ex.Message}");
            MessageBox.Show(this, ex.Message, "Ecosys",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task DisconnectAsync()
    {
        try
        {
            await transport.DisconnectAsync();
            SetConnection(null, "Verbindung getrennt");
            AddLog("Verbindung getrennt");
        }
        catch (Exception ex)
        {
            AddLog($"Trennen fehlgeschlagen: {ex.Message}");
        }
    }

    private void OnMessage(string raw)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => OnMessage(raw));
            return;
        }

        ReceivedMessage.TryParse(raw, out var received);
        var text = received is null
            ? $"← {Truncate(raw, 48)}"
            : $"← {received.Type}{(string.IsNullOrWhiteSpace(received.DeviceName) ? "" : " · " + received.DeviceName)}";

        SetStatus(text);
        AddLog(text + (received?.Sender is { Length: > 0 } sender ? $" ({Truncate(sender, 18)})" : ""));
    }

    private void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(message));
            return;
        }

        statusLabel.Text = message;
        var connected = transport.IsConnected;
        statusDot.Fill = connected ? Theme.Green : Color.FromArgb(120, 138, 148);
        sendButton.Enabled = connected;
        disconnectButton.Enabled = connected;
    }

    private void SetConnection(string? deviceName, string detail)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetConnection(deviceName, detail));
            return;
        }

        var connected = transport.IsConnected;
        connectionTitle.Text = deviceName is null ? "Kein Gerät verbunden" : deviceName;
        connectionDetail.Text = detail;
        sendButton.Enabled = connected;
        disconnectButton.Enabled = connected;
        statusDot.Fill = connected ? Theme.Green : Color.FromArgb(120, 138, 148);
    }

    private void AddLog(string message)
    {
        activity.Insert(0, $"{DateTime.Now:HH:mm:ss}  {message}");
        if (activity.Count > 6)
            activity.RemoveAt(activity.Count - 1);
        RefreshActivity();
    }

    private void RefreshActivity()
    {
        if (InvokeRequired)
        {
            BeginInvoke(RefreshActivity);
            return;
        }

        activityLabel.Text = activity.Count == 0
            ? "Noch keine Aktivität."
            : string.Join(Environment.NewLine, activity);
    }

    private static string Truncate(string value, int length) =>
        value.Length <= length ? value : value[..(length - 1)] + "…";

    private static void OpenSettings(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
            // Opening the Windows settings page is best effort only.
        }
    }

    private void LayoutChrome()
    {
        if (titleBar.Controls.Count < 3)
            return;

        // Window buttons were added in minimize, maximize, close order.
        var order = titleBar.Controls.OfType<RoundedButton>().ToList();
        var x = titleBar.ClientSize.Width - 12;
        for (var index = order.Count - 1; index >= 0; index--)
        {
            var button = order[index];
            x -= button.Width;
            button.Location = new Point(x, 8);
            x -= 4;
        }

        var ruleWidth = Math.Max(200, hero.ClientSize.Width - 52);
        statusRule.SetBounds(26, hero.ClientSize.Height - 58, ruleWidth, 1);
        statusDot.Location = new Point(28, hero.ClientSize.Height - 38);
        statusLabel.SetBounds(48, hero.ClientSize.Height - 46, Math.Max(120, ruleWidth - 30), 20);
    }

    private void UpdateMaximizedBounds()
    {
        var screen = Screen.FromControl(this);
        MaximizedBounds = screen.WorkingArea;
    }

    private void ToggleMaximize()
    {
        UpdateMaximizedBounds();
        WindowState = WindowState == FormWindowState.Maximized
            ? FormWindowState.Normal
            : FormWindowState.Maximized;
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        ReleaseCapture();
        SendMessage(Handle, 0xA1, 0x2, 0);
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        TryEnableRoundedCorners();
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
            return parameters;
        }
    }

    private void TryEnableRoundedCorners()
    {
        try
        {
            var preference = 2; // DWMWCP_ROUND
            _ = DwmSetWindowAttribute(Handle, 33, ref preference, sizeof(int));
        }
        catch
        {
            // Windows 10 does not support rounded window corners.
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_NCHITTEST = 0x0084;
        const int HTCLIENT = 1;

        base.WndProc(ref m);

        if (m.Msg != WM_NCHITTEST || (int)m.Result != HTCLIENT || WindowState != FormWindowState.Normal)
            return;

        var cursor = PointToClient(Cursor.Position);
        var border = Math.Max(4, LogicalToDeviceUnits(6));
        var left = cursor.X <= border;
        var right = cursor.X >= ClientSize.Width - border;
        var top = cursor.Y <= border;
        var bottom = cursor.Y >= ClientSize.Height - border;

        if (top && left) m.Result = (IntPtr)13;        // HTTOPLEFT
        else if (top && right) m.Result = (IntPtr)14;  // HTTOPRIGHT
        else if (bottom && left) m.Result = (IntPtr)16; // HTBOTTOMLEFT
        else if (bottom && right) m.Result = (IntPtr)17; // HTBOTTOMRIGHT
        else if (left) m.Result = (IntPtr)10;           // HTLEFT
        else if (right) m.Result = (IntPtr)11;          // HTRIGHT
        else if (top) m.Result = (IntPtr)12;            // HTTOP
        else if (bottom) m.Result = (IntPtr)15;         // HTBOTTOM
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, int wParam, int lParam);

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
