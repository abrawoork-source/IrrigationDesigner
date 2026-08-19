// File: src/IrrigationApp/App.xaml.cs
using IrrigationApp.Services;
using System.IO;
using System.Windows;

namespace IrrigationApp;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ── Database path: %AppData%\IrrigationDesigner\irrigation.db ─────────
        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "IrrigationDesigner");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "irrigation.db");

        var db = DatabaseService.Instance;
        db.Initialize(dbPath);
        await db.SeedDefaultDataAsync();

        // Show main window (already set in StartupUri, but we wire the ViewModel here)
    }

    protected override void OnExit(ExitEventArgs e)
    {
        base.OnExit(e);
    }
}
