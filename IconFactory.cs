using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace RUtils;

/// <summary>
/// Builds a distinctive tray icon at runtime (a blue disc with a white "R"),
/// so r-utils is easy to spot in the tray/overflow instead of showing the
/// generic Windows app icon. No external .ico file needed — good for
/// single-file publishing.
/// </summary>
public static class IconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    public static Icon CreateTrayIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var bg = new SolidBrush(Color.FromArgb(0, 120, 215)); // Windows blue
            g.FillEllipse(bg, 1, 1, 30, 30);

            using var pen = new Pen(Color.White, 1.5f);
            g.DrawEllipse(pen, 1, 1, 30, 30);

            using var font = new Font("Segoe UI", 18, FontStyle.Bold, GraphicsUnit.Pixel);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString("R", font, Brushes.White, new RectangleF(0, 0, 32, 32), sf);
        }

        IntPtr hIcon = bmp.GetHicon();
        try
        {
            // Clone into a managed icon so we can free the GDI handle immediately.
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            DestroyIcon(hIcon);
        }
    }
}
