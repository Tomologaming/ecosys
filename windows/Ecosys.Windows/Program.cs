using System.Diagnostics;
using Windows.Devices.Radios;
using Ecosys.Windows.Transport;

try
{
    ApplicationConfiguration.Initialize();
    Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
    Application.ThreadException += (_, e) => StartupDiagnostics.Log("UI thread exception", e.Exception);
    AppDomain.CurrentDomain.UnhandledException += (_, e) =>
    {
        if (e.ExceptionObject is Exception ex)
            StartupDiagnostics.Log("Unhandled application exception", ex);
    };
    Application.Run(new EcosysForm());
}
catch (Exception ex)
{
    StartupDiagnostics.Log("Startup exception", ex);
    if (!string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase))
    {
        MessageBox.Show(
            "Ecosys konnte nicht gestartet werden.\n\n" +
            "Details wurden unter %LOCALAPPDATA%\\Ecosys\\startup.log gespeichert.",
            "Ecosys Startfehler",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }
    Environment.ExitCode = 1;
}

sealed class EcosysForm : Form
{
    readonly BluetoothTransport transport = new();
    readonly FlowLayoutPanel devicesPanel = new();
    readonly Label bluetoothStatusLabel = new();
    readonly Label deviceCountLabel = new();
    readonly Button scanButton = new();
    readonly Button sendHelloButton = new();
    readonly Label connectionLabel = new();
    readonly Label messageLabel = new();

    static readonly Color Page = Color.FromArgb(245, 247, 249);
    static readonly Color Ink = Color.FromArgb(25, 35, 45);
    static readonly Color Muted = Color.FromArgb(90, 105, 115);
    static readonly Color Blue = Color.RoyalBlue;
    static readonly Color Green = Color.FromArgb(35, 145, 85);

    public EcosysForm()
    {
        Text = "Ecosys";
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F);
        ClientSize = new Size(900, 680);
        MinimumSize = new Size(760, 600);
        BackColor = Page;
        Icon = LoadAppIcon();

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Page,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 5
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 18)
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Ecosys",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 24F),
            ForeColor = Ink,
            Margin = new Padding(0, 0, 0, 2)
        };
        header.Controls.Add(title, 0, 0);

        var subtitle = new Label
        {
            Text = "Direkte Bluetooth-Verbindung zwischen deinen Geräten",
            AutoSize = true,
            Font = new Font("Segoe UI", 10F),
            ForeColor = Muted,
            Margin = new Padding(0, 0, 0, 0)
        };
        header.Controls.Add(subtitle, 0, 1);

        var settingsButton = new Button
        {
            Text = "Bluetooth-Einstellungen",
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Height = 38,
            Margin = new Padding(12, 3, 0, 0)
        };
        StyleSecondaryButton(settingsButton);
        settingsButton.Click += (_, _) => OpenSettings("ms-settings:bluetooth");
        header.Controls.Add(settingsButton, 1, 0);
        header.SetRowSpan(settingsButton, 2);
        root.Controls.Add(header, 0, 0);

        var statusBox = new GroupBox
        {
            Text = "Bluetooth",
            Dock = DockStyle.Top,
            Height = 76,
            Padding = new Padding(14, 10, 14, 8),
            Margin = new Padding(0, 0, 0, 14),
            ForeColor = Ink
        };
        bluetoothStatusLabel.Text = "Bluetooth wird geprüft …";
        bluetoothStatusLabel.Dock = DockStyle.Fill;
        bluetoothStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
        bluetoothStatusLabel.Font = new Font("Segoe UI Semibold", 10F);
        bluetoothStatusLabel.ForeColor = Ink;
        statusBox.Controls.Add(bluetoothStatusLabel);
        root.Controls.Add(statusBox, 0, 1);

        var devicesBox = new GroupBox
        {
            Text = "Ecosys-Geräte in der Nähe",
            Dock = DockStyle.Fill,
            Padding = new Padding(12, 24, 12, 12),
            Margin = new Padding(0, 0, 0, 14),
            ForeColor = Ink
        };

        var devicesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0),
            Padding = new Padding(0)
        };
        devicesLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        devicesLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        devicesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var searchRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 10)
        };
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        deviceCountLabel.Text = "Noch nicht gesucht";
        deviceCountLabel.AutoSize = true;
        deviceCountLabel.Anchor = AnchorStyles.Left;
        deviceCountLabel.Font = new Font("Segoe UI", 9F);
        deviceCountLabel.ForeColor = Muted;
        searchRow.Controls.Add(deviceCountLabel, 0, 0);

        scanButton.Text = "Suchen";
        scanButton.Size = new Size(110, 40);
        scanButton.Anchor = AnchorStyles.Right;
        scanButton.Margin = new Padding(8, 0, 0, 0);
        StylePrimaryButton(scanButton);
        scanButton.Click += async (_, _) => await ScanAsync();
        searchRow.Controls.Add(scanButton, 1, 0);

        devicesLayout.Controls.Add(searchRow, 0, 0);

        devicesPanel.FlowDirection = FlowDirection.TopDown;
        devicesPanel.WrapContents = false;
        devicesPanel.AutoScroll = true;
        devicesPanel.Dock = DockStyle.Fill;
        devicesPanel.BackColor = Color.White;
        devicesPanel.BorderStyle = BorderStyle.FixedSingle;
        devicesPanel.Padding = new Padding(8);
        devicesPanel.Resize += (_, _) => ResizeDeviceRows();
        devicesLayout.Controls.Add(devicesPanel, 0, 1);

        devicesBox.Controls.Add(devicesLayout);
        root.Controls.Add(devicesBox, 0, 2);

        var connectionBox = new GroupBox
        {
            Text = "Verbindung",
            Dock = DockStyle.Top,
            Height = 126,
            Padding = new Padding(14, 24, 14, 12),
            Margin = new Padding(0, 0, 0, 14),
            ForeColor = Ink
        };

        var connectionLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        connectionLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        connectionLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        connectionLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        connectionLabel.Text = "Kein Gerät verbunden";
        connectionLabel.AutoSize = true;
        connectionLabel.Font = new Font("Segoe UI Semibold", 10F);
        connectionLabel.ForeColor = Ink;
        connectionLayout.Controls.Add(connectionLabel, 0, 0);

        sendHelloButton.Text = "➤   Hello senden";
        sendHelloButton.Dock = DockStyle.Fill;
        sendHelloButton.Height = 48;
        sendHelloButton.Margin = new Padding(0, 10, 0, 0);
        sendHelloButton.Font = new Font("Segoe UI Semibold", 12F);
        sendHelloButton.TextAlign = ContentAlignment.MiddleCenter;
        sendHelloButton.Enabled = false;
        StyleSendButton(sendHelloButton);
        sendHelloButton.Click += async (_, _) => await SendHelloAsync();
        connectionLayout.Controls.Add(sendHelloButton, 0, 1);

        connectionBox.Controls.Add(connectionLayout);
        root.Controls.Add(connectionBox, 0, 3);

        messageLabel.Text = "Bereit";
        messageLabel.Dock = DockStyle.Fill;
        messageLabel.AutoSize = false;
        messageLabel.Height = 28;
        messageLabel.TextAlign = ContentAlignment.MiddleLeft;
        messageLabel.Font = new Font("Segoe UI", 9F);
        messageLabel.ForeColor = Muted;
        root.Controls.Add(messageLabel, 0, 4);

        transport.StatusChanged += (_, message) => SetStatus(message);
        transport.DevicesChanged += (_, devices) => ShowDevices(devices);
        transport.MessageReceived += (_, message) => SetStatus("Empfangen: " + message);

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
                SetStatus("Bluetooth ist deaktiviert.");
                var result = MessageBox.Show(
                    this,
                    "Ecosys benötigt Bluetooth, um Geräte zu finden. Soll die Windows-Bluetooth-Einstellungsseite geöffnet werden?",
                    "Bluetooth benötigt",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (result == DialogResult.Yes)
                    OpenSettings("ms-settings:bluetooth");
                return;
            }

            await transport.ScanAsync();
        }
        catch (UnauthorizedAccessException)
        {
            SetStatus("Bluetooth-Zugriff nicht erlaubt.");
            MessageBox.Show(
                this,
                "Windows hat den Bluetooth-Zugriff für Ecosys nicht freigegeben. Bitte Bluetooth bzw. die Geräteberechtigung in den Windows-Einstellungen prüfen.",
                "Bluetooth-Zugriff",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            SetStatus("Bluetooth-Suche fehlgeschlagen.");
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
        if (InvokeRequired)
        {
            BeginInvoke(() => ShowDevices(devices));
            return;
        }

        devicesPanel.SuspendLayout();
        devicesPanel.Controls.Clear();

        deviceCountLabel.Text = devices.Count == 0
            ? "Keine Ecosys-Geräte gefunden"
            : $"{devices.Count} Gerät{(devices.Count == 1 ? "" : "e")} gefunden";

        foreach (var device in devices)
        {
            var row = new Panel
            {
                Height = 62,
                Width = Math.Max(400, devicesPanel.ClientSize.Width - 26),
                BackColor = Color.White,
                Margin = new Padding(0, 0, 0, 8),
                BorderStyle = BorderStyle.FixedSingle
            };

            var name = new Label
            {
                Text = device.Name,
                AutoEllipsis = true,
                AutoSize = false,
                Location = new Point(14, 8),
                Size = new Size(Math.Max(180, row.Width - 160), 23),
                Font = new Font("Segoe UI Semibold", 10F),
                ForeColor = Ink
            };
            row.Controls.Add(name);

            var detail = new Label
            {
                Text = "Ecosys Bluetooth",
                AutoSize = true,
                Location = new Point(14, 33),
                Font = new Font("Segoe UI", 8.5F),
                ForeColor = Muted
            };
            row.Controls.Add(detail);

            var connect = new Button
            {
                Text = "Verbinden",
                Size = new Size(110, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(Math.Max(180, row.Width - 124), 11),
                Font = new Font("Segoe UI Semibold", 9F),
                Margin = new Padding(0)
            };
            StylePrimaryButton(connect);
            connect.Click += async (_, _) => await ConnectAsync(device, connect);
            row.Controls.Add(connect);

            devicesPanel.Controls.Add(row);
        }

        devicesPanel.ResumeLayout();
        ResizeDeviceRows();
    }

    void ResizeDeviceRows()
    {
        foreach (Control control in devicesPanel.Controls)
        {
            if (control is not Panel row)
                continue;

            row.Width = Math.Max(400, devicesPanel.ClientSize.Width - 26);

            foreach (Control child in row.Controls)
            {
                if (child is Label label && label.Text != "Ecosys Bluetooth")
                    label.Width = Math.Max(180, row.Width - 160);

                if (child is Button button)
                    button.Left = Math.Max(180, row.Width - button.Width - 12);
            }
        }
    }

    async Task ConnectAsync(BluetoothDeviceInfo device, Button button)
    {
        button.Enabled = false;
        button.Text = "Verbinde …";
        SetStatus($"Verbinde mit {device.Name} …");

        try
        {
            await transport.ConnectAsync(device);
            connectionLabel.Text = $"Verbunden mit {device.Name}";
            sendHelloButton.Enabled = true;
            SetStatus($"Verbunden mit {device.Name}");
        }
        catch (Exception ex)
        {
            connectionLabel.Text = "Verbindung fehlgeschlagen";
            SetStatus("Verbindung fehlgeschlagen.");
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

            SetStatus("Hello erfolgreich gesendet.");
        }
        catch (Exception ex)
        {
            SetStatus("Senden fehlgeschlagen.");
            MessageBox.Show(this, ex.Message, "Ecosys", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void SetStatus(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(() => SetStatus(message));
            return;
        }

        bluetoothStatusLabel.Text = message;
        messageLabel.Text = message;
    }

    static void StylePrimaryButton(Button button)
    {
        button.BackColor = Blue;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(55, 105, 210);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(35, 80, 175);
        button.Cursor = Cursors.Hand;
    }

    static void StyleSecondaryButton(Button button)
    {
        button.BackColor = Color.White;
        button.ForeColor = Ink;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(190, 200, 208);
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(235, 240, 245);
        button.Cursor = Cursors.Hand;
    }

    static void StyleSendButton(Button button)
    {
        button.BackColor = Color.RoyalBlue;
        button.ForeColor = Color.White;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(65, 105, 225);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(35, 75, 180);
        button.Cursor = Cursors.Hand;
    }

    static void OpenSettings(string uri)
    {
        try
        {
            Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true });
        }
        catch
        {
        }
    }

    static Icon LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "ecosys.ico");
        try
        {
            return File.Exists(path) ? new Icon(path) : SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }
}

static class StartupDiagnostics
{
    public static void Log(string source, Exception exception)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ecosys");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "startup.log");
            File.AppendAllText(
                path,
                $"[{DateTime.Now:O}] {source}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Never allow diagnostics to become another startup failure.
        }
    }
}
