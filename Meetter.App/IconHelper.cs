using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Meetter.App;

internal static class IconHelper
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static ImageSource CreateWindowIcon()
    {
        var ico = AppIconFactory.CreateIcon();
        var hIcon = ico.Handle;
        var source = Imaging.CreateBitmapSourceFromHIcon(hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        try { DestroyIcon(hIcon); } catch { }
        return source;
    }
}

