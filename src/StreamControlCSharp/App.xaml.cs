using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace StreamControlCSharp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "startup.log");

    public static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}{Environment.NewLine}");
        }
        catch
        {
            // No bloquear el arranque por un error de log.
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        Log("OnStartup");
        DispatcherUnhandledException += (_, args) =>
        {
            Log("DispatcherUnhandledException: " + args.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log("UnhandledException: " + args.ExceptionObject);
        };

        try
        {
            base.OnStartup(e);
            Log($"After base.OnStartup; windows={Windows.Count}");
        }
        catch (Exception ex)
        {
            Log("OnStartup exception: " + ex);
            MessageBox.Show(ex.ToString(), "StreamControlC# - Error de arranque");
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log("OnExit: " + e.ApplicationExitCode);
        base.OnExit(e);
    }
}
