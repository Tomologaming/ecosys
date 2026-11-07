using System.Drawing.Drawing2D;

namespace Ecosys.Windows.UI;

internal static class Theme
{
    public static readonly Color Ink = Color.FromArgb(24, 32, 40);
    public static readonly Color Muted = Color.FromArgb(108, 120, 128);
    public static readonly Color Faint = Color.FromArgb(148, 159, 167);
    public static readonly Color Page = Color.FromArgb(240, 243, 245);
    public static readonly Color Card = Color.White;
    public static readonly Color Line = Color.FromArgb(226, 232, 235);
    public static readonly Color Navy = Color.FromArgb(15, 33, 48);
    public static readonly Color NavyDeep = Color.FromArgb(10, 22, 34);
    public static readonly Color Teal = Color.FromArgb(26, 82, 74);
    public static readonly Color Blue = Color.FromArgb(47, 111, 176);
    public static readonly Color BlueHover = Color.FromArgb(38, 96, 156);
    public static readonly Color Green = Color.FromArgb(54, 158, 104);
    public static readonly Color GreenHover = Color.FromArgb(45, 139, 90);
    public static readonly Color MintTint = Color.FromArgb(233, 245, 239);
    public static readonly Color MintLine = Color.FromArgb(200, 228, 213);

    public static readonly Font Body = new("Segoe UI", 9.5f);
    public static readonly Font BodySmall = new("Segoe UI", 8.5f);
    public static readonly Font BodyTiny = new("Segoe UI", 8f);
    public static readonly Font Semibold = new("Segoe UI Semibold", 9.5f);
    public static readonly Font CardTitle = new("Segoe UI Semibold", 11f);
    public static readonly Font Section = new("Segoe UI Semibold", 8f);
    public static readonly Font Hero = new("Segoe UI Semibold", 25f);
    public static readonly Font HeroSub = new("Segoe UI", 9f);
    public static readonly Font Mono = new("Consolas", 8.5f);

    public static GraphicsPath RoundRect(RectangleF bounds, float radius)
    {
        var path = new GraphicsPath();
        var d = Math.Max(1f, Math.Min(radius * 2f, Math.Min(bounds.Width, bounds.Height)));
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
