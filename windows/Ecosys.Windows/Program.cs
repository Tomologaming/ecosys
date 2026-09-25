using System.Windows.Forms;
using Ecosys.Windows.Transport;

ApplicationConfiguration.Initialize();
Application.Run(new EcosysForm());

sealed class EcosysForm : Form
{
    private readonly BluetoothTransport transport = new();
    private readonly Label status = new();
    private readonly Label deviceStatus = new();
    private readonly ListBox devices = new();
    private readonly Button scan = new();
    private readonly Button hello = new();

    public EcosysForm()
    {
        Text = "Ecosys";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(620, 720);
        Size = new Size(720, 820);
        BackColor = Color.FromArgb(247, 248, 250);
        Font = new Font("Segoe UI", 10);

        var content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(28) };
        Controls.Add(content);

        var title = new Label {
            Text = "Ecosys", AutoSize = true, Font = new Font("Segoe UI Semibold", 28),
            ForeColor = Color.FromArgb(22, 24, 29), Location = new Point(28, 24)
        };
        content.Controls.Add(title);

        var tagline = new Label {
            Text = "Private. Direct. Yours.", AutoSize = true,
            ForeColor = Color.FromArgb(105, 110, 120), Location = new Point(30, 70)
        };
        content.Controls.Add(tagline);

        var statusCard = new Panel {
            BackColor = Color.White, Location = new Point(28, 108), Size = new Size(636, 64)
        };
        statusCard.Paint += (_, e) => ControlPaint.DrawBorder(e.Graphics, statusCard.ClientRectangle,
            Color.FromArgb(230, 232, 236), ButtonBorderStyle.Solid);
        content.Controls.Add(statusCard);

        var dot = new Panel { BackColor = Color.FromArgb(155, 160, 170), Size = new Size(12, 12), Location = new Point(20, 26) };
        statusCard.Controls.Add(dot);
        status.Text = "Bluetooth starting…";
        status.AutoSize = true;
        status.Location = new Point(44, 21);
        status.ForeColor = Color.FromArgb(55, 59, 68);
        statusCard.Controls.Add(status);

        var thisDevice = SectionTitle("This device", 28, 192, content);
        var deviceCard = new Panel { BackColor = Color.White, Location = new Point(28, 228), Size = new Size(636, 92) };
        content.Controls.Add(deviceCard);

        deviceStatus.Text = Environment.MachineName;
        deviceStatus.Font = new Font("Segoe UI Semibold", 15);
        deviceStatus.Location = new Point(18, 16);
        deviceStatus.AutoSize = true;
        deviceCard.Controls.Add(deviceStatus);
        deviceCard.Controls.Add(new Label {
            Text = "Windows • This device", AutoSize = true, Location = new Point(18, 48),
            ForeColor = Color.FromArgb(105, 110, 120)
        });

        SectionTitle("Nearby devices", 28, 344, content);
        devices.Location = new Point(28, 380);
        devices.Size = new Size(636, 170);
        devices.BorderStyle = BorderStyle.FixedSingle;
        devices.BackColor = Color.White;
        devices.HorizontalScrollbar = true;
        content.Controls.Add(devices);

        scan.Text = "Find nearby devices";
        scan.Location = new Point(28, 565);
        scan.Size = new Size(306, 48);
        scan.BackColor = Color.FromArgb(42, 91, 220);
        scan.ForeColor = Color.White;
        scan.FlatStyle = FlatStyle.Flat;
        scan.Click += async (_, _) => await ScanAsync();
        content.Controls.Add(scan);

        var refresh = new Button {
            Text = "Refresh", Location = new Point(358, 565), Size = new Size(140, 48),
            FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Color.FromArgb(42, 91, 220)
        };
        refresh.Click += async (_, _) => await ScanAsync();
        content.Controls.Add(refresh);

        hello.Text = "Send Hello";
        hello.Location = new Point(28, 628);
        hello.Size = new Size(636, 48);
        hello.FlatStyle = FlatStyle.Flat;
        hello.BackColor = Color.White;
        hello.ForeColor = Color.FromArgb(42, 91, 220);
        hello.Click += async (_, _) => {
            await transport.SendAsync(Ecosys.Windows.Protocol.EcosysMessage.Hello(
                $"windows-{Environment.MachineName}", Environment.MachineName, "windows").ToJsonString());
            status.Text = "Hello sent";
        };
        content.Controls.Add(hello);

        content.Controls.Add(new Label {
            Text = "Ecosys connects your devices directly. Your data stays between your devices.",
            AutoSize = false, TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(125, 129, 138), Location = new Point(40, 690), Size = new Size(610, 45)
        });

        transport.StatusChanged += (_, message) => SetStatus(message);
        transport.MessageReceived += (_, message) => SetStatus($"Received: {message}");

        Shown += async (_, _) => await ScanAsync();
        FormClosed += async (_, _) => await transport.DisposeAsync();
    }

    private async Task ScanAsync()
    {
        scan.Enabled = false;
        devices.Items.Clear();
        status.Text = "Scanning for nearby Ecosys devices…";
        try
        {
            await transport.StartAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"Scan failed: {ex.Message}");
        }
        finally
        {
            scan.Enabled = true;
        }
    }

    private void SetStatus(string text)
    {
        if (InvokeRequired) { BeginInvoke(() => SetStatus(text)); return; }
        status.Text = text;
        devices.Items.Add(text);
        if (devices.Items.Count > 100) devices.Items.RemoveAt(0);
    }

    private static Label SectionTitle(string text, int x, int y, Control parent)
    {
        var label = new Label {
            Text = text, AutoSize = true, Location = new Point(x, y),
            Font = new Font("Segoe UI Semibold", 14), ForeColor = Color.FromArgb(35, 38, 45)
        };
        parent.Controls.Add(label);
        return label;
    }
}
