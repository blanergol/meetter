using System.Windows;
using Meetter.Core;

namespace Meetter.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        Logger.Initialize();
        Logger.Info("Application startup");
        base.OnStartup(e);
    }
}

