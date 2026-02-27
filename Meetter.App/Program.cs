namespace Meetter.App;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        AppLogger.Configure();
        AppLogger.Info("Application start");
        Meetter.Core.Logger.Initialize(AppLogger.Info, AppLogger.Error);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) =>
        {
            AppLogger.Error("Unhandled UI thread exception", e.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLogger.Error($"Unhandled AppDomain exception (terminating={e.IsTerminating})", ex);
                return;
            }

            AppLogger.Error($"Unhandled AppDomain exception (non-Exception object, terminating={e.IsTerminating})");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            AppLogger.Error("Unobserved task exception", e.Exception);
            e.SetObserved();
        };

        ApplicationConfiguration.Initialize();
        Application.ApplicationExit += (_, __) =>
        {
            try
            {
                AppLogger.Info("Application exit");
            }
            catch
            {
            }
        };
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
        }
    }
}
