using System.Windows;
using System.Windows.Threading;

namespace SessionManagement.Admin
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Admin Fatal Exception] {e.ExceptionObject}");
            // Optional: log to a file here if needed
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Admin Unhandled Exception] {e.Exception}");
            e.Handled = true; // Prevent process exit on unhandled UI exceptions
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[Admin Unobserved Task Exception] {e.Exception}");
            e.SetObserved();
        }
    }
}

