using System.Drawing.Drawing2D;
using System.Reflection;
using Ecosys.Windows.Transport;

namespace Ecosys.Windows.UI;

internal static class LogoArt
{
    private static Image? _image;
    private static Icon? _icon;

    public static Image Image => _image ??= LoadImage();

    public static Icon AppIcon => _icon ??= LoadIcon();

    private static Stream? Open(string name) =>
        Assembly.GetExecutingAssembly().GetManifestResourceStream(name);

    private static Image LoadImage()
    {
        using var stream = Open("Ecosys.Windows.Assets.ecosys.png");
        if (stream is null)
            return new Bitmap(1, 1);
        using var source = Image.FromStream(stream);
        return new Bitmap(source);
    }

    private static Icon LoadIcon()
    {
        using var stream = Open("Ecosys.Windows.Assets.ecosys.ico");
        return stream is null ? SystemIcons.Application : new Icon(stream);
    }
}

/// <summary>Panel that paints a rounded, optionally bordered surface in its background pass,
/// so transparent child labels pick up the card colour instead of the page colour.</summary>
internal class RoundedPanel : Panel
{
    public Color Fill { get; set; } = Theme.Card;
    public Color BorderColor { get; set; } = Theme.Line;
    public int Radius { get; set; } = 14;
    public bool ShowBorder { get; set; } = true;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
    }

    private Color SurfaceColor
    {
        get
        {
            var color = Parent?.BackColor ?? Theme.Page;
            return color.A == 0 ? Theme.Page : color;
        }
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(SurfaceColor);
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        if (bounds.Width <= 1 || bounds.Height <= 1)
            return;
        using var path = Theme.RoundRect(bounds, Radius);
        using var brush = new SolidBrush(Fill);
        e.Graphics.FillPath(brush, path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (!ShowBorder || BorderColor.A == 0)
            return;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        using var path = Theme.RoundRect(bounds, Radius);
        using var pen = new Pen(BorderColor);
        e.Graphics.DrawPath(pen, path);
    }
}

internal sealed class RoundedButton : Button
{
    private bool hovered;
    private bool pressed;

    public Color FillColor { get; set; } = Theme.Blue;
    public Color HoverColor { get; set; } = Theme.BlueHover;
    public Color PressedColor { get; set; } = Theme.BlueHover;
    public Color TextColor { get; set; } = Color.White;
    public Color HoverTextColor { get; set; } = Color.White;
    public Color OutlineColor { get; set; } = Color.Transparent;
    public int Radius { get; set; } = 10;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        UseVisualStyleBackColor = false;
        Cursor = Cursors.Hand;
        Font = Theme.Semibold;
        BackColor = Theme.Card;
        Size = new Size(120, 34);
    }

    private static Color Blend(Color source, Color target, float amount) =>
        Color.FromArgb(
            (int)(source.R + (target.R - source.R) * amount),
            (int)(source.G + (target.G - source.G) * amount),
            (int)(source.B + (target.B - source.B) * amount));

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(BackColor);

        var bounds = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
        var fill = !Enabled
            ? Blend(FillColor, BackColor, 0.55f)
            : pressed
                ? PressedColor
                : hovered
                    ? HoverColor
                    : FillColor;

        using (var path = Theme.RoundRect(bounds, Radius))
        using (var brush = new SolidBrush(fill))
        {
            e.Graphics.FillPath(brush, path);
            if (OutlineColor.A > 0)
            {
                using var pen = new Pen(OutlineColor);
                e.Graphics.DrawPath(pen, path);
            }
        }

        var textColor = !Enabled ? Theme.Faint : hovered ? HoverTextColor : TextColor;
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            new Rectangle(0, 0, Width, Height),
            textColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis |
            TextFormatFlags.NoPrefix);

        if (Focused && ShowFocusCues)
            ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(4, 4, Width - 9, Height - 9));
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        hovered = false;
        pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        pressed = true;
        Invalidate();
        base.OnMouseDown(e);
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        pressed = false;
        Invalidate();
        base.OnMouseUp(e);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }
}

internal sealed class Dot : Control
{
    private Color fill = Theme.Green;

    public Color Fill
    {
        get => fill;
        set
        {
            fill = value;
            Invalidate();
        }
    }

    public Dot()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.SupportsTransparentBackColor,
            true);
        Size = new Size(9, 9);
        BackColor = Color.Transparent;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Fill);
        e.Graphics.FillEllipse(brush, 0, 0, Width - 1, Height - 1);
    }
}

internal sealed class IconTile : RoundedPanel
{
    public IconTile(int size = 40, int radius = 12)
    {
        Size = new Size(size, size);
        Radius = radius;
        Fill = Color.White;
        BorderColor = Theme.Line;
        var picture = new PictureBox
        {
            Image = LogoArt.Image,
            SizeMode = PictureBoxSizeMode.Zoom,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent
        };
        Padding = new Padding(4);
        Controls.Add(picture);
    }
}

internal sealed class GradientHeader : Panel
{
    public GradientHeader()
    {
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Theme.Navy;
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        var graphics = e.Graphics;
        graphics.SmoothingMode = SmoothingMode.AntiAlias;

        using (var brush = new LinearGradientBrush(
                   new Rectangle(0, 0, Math.Max(1, Width), Math.Max(1, Height)),
                   Theme.NavyDeep,
                   Theme.Teal,
                   18f))
        {
            graphics.FillRectangle(brush, ClientRectangle);
        }

        using (var glow = new SolidBrush(Color.FromArgb(30, 255, 255, 255)))
            graphics.FillEllipse(glow, Width - 190, -120, 320, 320);
        using (var glow = new SolidBrush(Color.FromArgb(22, 89, 231, 183)))
            graphics.FillEllipse(glow, -110, Height - 130, 260, 260);
    }
}

/// <summary>Vertically stacked, scrollable list of cards with automatic width handling.</summary>
internal sealed class CardStack : Panel
{
    private readonly List<(Control Control, int Gap)> items = new();

    public CardStack()
    {
        DoubleBuffered = true;
        AutoScroll = true;
        BackColor = Theme.Page;
        Padding = new Padding(0);
    }

    public void AddItem(Control control, int gap = 12)
    {
        items.Add((control, gap));
        Controls.Add(control);
        Relayout();
    }

    public void RemoveItem(Control control)
    {
        items.RemoveAll(item => item.Control == control);
        Controls.Remove(control);
        control.Dispose();
        Relayout();
    }

    public void Relayout()
    {
        var width = ClientSize.Width - Padding.Horizontal;
        if (VerticalScroll.Visible)
            width -= SystemInformation.VerticalScrollBarWidth;
        width = Math.Max(260, width);

        var y = Padding.Top;
        foreach (var (control, gap) in items)
        {
            control.SetBounds(Padding.Left, y, width, control.Height);
            y += control.Height + gap;
        }

        AutoScrollMinSize = new Size(0, y + Padding.Bottom);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        Relayout();
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);
        if (Visible)
            Relayout();
    }
}

internal sealed class DeviceListPanel : Panel
{
    private readonly List<BluetoothDeviceInfo> devices = new();
    private const int RowHeight = 68;
    private const int RowGap = 8;

    public event EventHandler<BluetoothDeviceInfo>? ConnectRequested;

    public string EmptyTitle { get; set; } = "Noch nicht gesucht";
    public string EmptyDetail { get; set; } = "Tippe auf „Suchen“, um Ecosys-Geräte in der Nähe zu finden.";
    public bool HasDevices => devices.Count > 0;

    public DeviceListPanel()
    {
        DoubleBuffered = true;
        BackColor = Theme.Page;
    }

    public void SetDevices(IReadOnlyList<BluetoothDeviceInfo> items)
    {
        devices.Clear();
        devices.AddRange(items);
        Rebuild();
    }

    private void Rebuild()
    {
        SuspendLayout();
        foreach (Control control in Controls)
            control.Dispose();
        Controls.Clear();

        if (devices.Count == 0)
        {
            var empty = BuildEmptyCard();
            empty.SetBounds(0, 0, Math.Max(260, Width), 82);
            empty.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            Controls.Add(empty);
            Height = 82;
        }
        else
        {
            for (var index = 0; index < devices.Count; index++)
                Controls.Add(BuildRow(devices[index], index));
            Height = devices.Count * RowHeight + (devices.Count - 1) * RowGap;
        }

        ResumeLayout(false);
        LayoutRows();
    }

    private RoundedPanel BuildEmptyCard()
    {
        var card = new RoundedPanel { Radius = 14, Height = 82 };
        var title = new Label
        {
            Text = EmptyTitle,
            Font = Theme.Semibold,
            ForeColor = Theme.Ink,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(18, 18)
        };
        var detail = new Label
        {
            Text = EmptyDetail,
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            BackColor = Color.Transparent,
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            Location = new Point(18, 42)
        };
        card.Controls.Add(title);
        card.Controls.Add(detail);
        return card;
    }

    private RoundedPanel BuildRow(BluetoothDeviceInfo device, int index)
    {
        var card = new RoundedPanel { Radius = 14, Height = RowHeight, Width = 560 };
        card.Controls.Add(new IconTile(38, 11) { Location = new Point(14, 15) });

        var name = new Label
        {
            Text = device.Name,
            Font = Theme.CardTitle,
            ForeColor = Theme.Ink,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(64, 12)
        };
        var detail = new Label
        {
            Text = device.Detail,
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            BackColor = Color.Transparent,
            AutoSize = true,
            Location = new Point(64, 34)
        };

        var connect = new RoundedButton
        {
            Text = "Verbinden",
            Size = new Size(100, 34),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(420, 17),
            FillColor = Theme.Blue,
            HoverColor = Theme.BlueHover,
            TextColor = Color.White,
            Radius = 9,
            Tag = device
        };
        connect.Click += (_, _) => ConnectRequested?.Invoke(this, device);

        card.Controls.Add(name);
        card.Controls.Add(detail);
        card.Controls.Add(connect);
        card.Tag = index;
        return card;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutRows();
    }

    private void LayoutRows()
    {
        if (devices.Count == 0)
        {
            foreach (Control control in Controls)
                control.Width = Math.Max(260, Width);
            return;
        }

        var y = 0;
        foreach (Control control in Controls)
        {
            control.SetBounds(0, y, Math.Max(260, Width), RowHeight);
            y += RowHeight + RowGap;
        }
    }
}
