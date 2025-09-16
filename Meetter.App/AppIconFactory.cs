using System.Drawing.Drawing2D;

namespace Meetter.App;

internal static class AppIconFactory
{
    public static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var bg = new LinearGradientBrush(new Rectangle(0, 0, 32, 32), Color.FromArgb(0x0B, 0x1E, 0x39),
                Color.FromArgb(0x1F, 0x6F, 0xEB), LinearGradientMode.ForwardDiagonal);
            g.FillRectangle(bg, 0, 0, 32, 32);
            using var brush = new SolidBrush(Color.White);
            using var font = new Font("Segoe UI Black", 16, FontStyle.Bold, GraphicsUnit.Pixel);
            var text = "M";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, brush, (32 - size.Width) / 2f, (32 - size.Height) / 2f - 1f);
        }

        return Icon.FromHandle(bmp.GetHicon());
    }
}