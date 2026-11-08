using Ecosys.Windows.Sync;

namespace Ecosys.Windows.UI;

/// <summary>Card listing remembered peers with their confirmation state.</summary>
internal sealed class KnownPeersCard : RoundedPanel
{
    private const int RowHeight = 54;
    private const int RowGap = 6;
    private const int HeaderHeight = 64;
    private const int FooterHeight = 44;

    private readonly Label detail = new();
    private readonly Panel rows = new();
    private readonly RoundedButton clearButton = new();

    private int rowsHeight;

    public event EventHandler<string>? TrustToggled;
    public event EventHandler? ClearRequested;

    public KnownPeersCard()
    {
        Radius = 14;

        var title = new Label
        {
            Text = "Frühere Verbindungen",
            Font = Theme.CardTitle,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(16, 14),
            BackColor = Color.Transparent
        };

        detail.Text = "Noch keine Geräte bekannt.";
        detail.Font = Theme.BodySmall;
        detail.ForeColor = Theme.Muted;
        detail.AutoSize = false;
        detail.AutoEllipsis = true;
        detail.BackColor = Color.Transparent;

        rows.BackColor = Color.Transparent;

        clearButton.Text = "Verlauf leeren";
        clearButton.Size = new Size(146, 32);
        clearButton.FillColor = Color.FromArgb(232, 238, 242);
        clearButton.HoverColor = Color.FromArgb(219, 229, 235);
        clearButton.PressedColor = Color.FromArgb(208, 219, 226);
        clearButton.TextColor = Theme.Ink;
        clearButton.HoverTextColor = Theme.Ink;
        clearButton.Radius = 9;
        clearButton.Font = Theme.Semibold;
        clearButton.Click += (_, _) => ClearRequested?.Invoke(this, EventArgs.Empty);

        Controls.Add(title);
        Controls.Add(detail);
        Controls.Add(rows);
        Controls.Add(clearButton);

        LayoutCard();
    }

    public void Refresh(IReadOnlyList<KnownPeer> peers, string? connectedId)
    {
        var trusted = peers.Count(peer => peer.Trusted);
        detail.Text = peers.Count == 0
            ? "Noch keine Geräte bekannt. Nach einem Hello trägt sich die Gegenstelle hier ein."
            : $"{peers.Count} Gerät{(peers.Count == 1 ? "" : "e")} im Verlauf · {trusted} bestätigt";

        foreach (Control control in rows.Controls)
            control.Dispose();
        rows.Controls.Clear();

        var y = 0;
        foreach (var peer in peers)
        {
            var row = BuildRow(peer, connectedId == peer.Id);
            row.Location = new Point(0, y);
            rows.Controls.Add(row);
            y += RowHeight + RowGap;
        }

        rowsHeight = peers.Count == 0 ? 0 : peers.Count * (RowHeight + RowGap) - RowGap;
        LayoutCard();
    }

    private static string Meta(KnownPeer peer, bool isConnected)
    {
        var platform = peer.Platform switch
        {
            "windows" => "Windows",
            "android" => "Android",
            "ios" => "iOS",
            "macos" => "macOS",
            _ => "Unbekannt"
        };

        var seen = KnownPeers.DescribeLastSeen(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), peer.LastSeen);
        var parts = new List<string> { platform, $"zuletzt {seen}" };
        if (peer.Trusted)
            parts.Add("bestätigt");
        if (isConnected)
            parts.Add("verbunden");
        return string.Join(" · ", parts);
    }

    private Panel BuildRow(KnownPeer peer, bool isConnected)
    {
        var row = new Panel { Height = RowHeight, BackColor = Color.Transparent };

        var name = new Label
        {
            Text = peer.Name,
            Font = Theme.Semibold,
            ForeColor = Theme.Ink,
            AutoSize = true,
            Location = new Point(16, 7),
            BackColor = Color.Transparent
        };

        var meta = new Label
        {
            Text = Meta(peer, isConnected),
            Font = Theme.BodySmall,
            ForeColor = Theme.Muted,
            AutoSize = true,
            Location = new Point(16, 28),
            BackColor = Color.Transparent
        };

        var toggle = new RoundedButton
        {
            Text = peer.Trusted ? "Bestätigung entziehen" : "Vertrauen",
            Size = new Size(peer.Trusted ? 172 : 96, 30),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            FillColor = peer.Trusted ? Color.FromArgb(232, 245, 238) : Theme.Blue,
            HoverColor = peer.Trusted ? Color.FromArgb(219, 238, 228) : Theme.BlueHover,
            PressedColor = peer.Trusted ? Color.FromArgb(208, 232, 219) : Theme.BlueHover,
            TextColor = peer.Trusted ? Color.FromArgb(28, 92, 68) : Color.White,
            HoverTextColor = peer.Trusted ? Color.FromArgb(28, 92, 68) : Color.White,
            OutlineColor = peer.Trusted ? Theme.MintLine : Color.Transparent,
            Radius = 8,
            Tag = peer.Id
        };
        var id = peer.Id;
        toggle.Click += (_, _) => TrustToggled?.Invoke(this, id);

        row.Controls.Add(name);
        row.Controls.Add(meta);
        row.Controls.Add(toggle);
        return row;
    }

    private void LayoutCard()
    {
        var width = Math.Max(280, Width);

        detail.SetBounds(16, 38, Math.Max(120, width - 32), 18);
        rows.SetBounds(0, HeaderHeight, width, Math.Max(0, rowsHeight));

        var footerTop = HeaderHeight + rowsHeight + (rowsHeight > 0 ? 10 : 2);
        clearButton.Location = new Point(16, footerTop);

        foreach (Control row in rows.Controls)
        {
            row.SetBounds(0, row.Top, width, RowHeight);
            if (row.Controls.Count == 3)
                row.Controls[2].Location = new Point(width - row.Controls[2].Width - 8, 12);
        }

        var height = footerTop + 32 + 16;
        if (Height != height)
            Height = height;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        LayoutCard();
    }
}
