namespace Meetter.App;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        AppLogger.Configure();
        AppLogger.Info("Application start");
        Meetter.Core.Logger.Initialize(AppLogger.Info, AppLogger.Error);

        ApplicationConfiguration.Initialize();
        try
        {
            var args = Environment.GetCommandLineArgs();
            var isAutoStart = Array.Exists(args,
                a => string.Equals(a, "--autostart", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm
            {
                WindowState = isAutoStart ? FormWindowState.Minimized : FormWindowState.Normal,
                ShowInTaskbar = !isAutoStart
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Fatal UI exception", ex);

            try
            {
                MessageBox.Show(ex.ToString(), "Fatal error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception uiEx)
            {
                System.Diagnostics.Debug.WriteLine(uiEx.ToString());
            }
        }
    }
}