using System;
using System.Windows.Forms;

namespace Meetter.WinForms;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        try
        {
            AppLogger.Configure();
            AppLogger.Info("Application start");
        }
        catch { }
        ApplicationConfiguration.Initialize();
        try
        {
            var args = Environment.GetCommandLineArgs();
            var isAutoStart = Array.Exists(args, a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm { WindowState = isAutoStart ? FormWindowState.Minimized : FormWindowState.Normal, ShowInTaskbar = !isAutoStart });
        }
        catch (Exception ex)
        {
            try { AppLogger.Error("Fatal UI exception", ex); } catch { }
            try { MessageBox.Show(ex.ToString(), "Fatal error", MessageBoxButtons.OK, MessageBoxIcon.Error); } catch { }
        }
    }
}

