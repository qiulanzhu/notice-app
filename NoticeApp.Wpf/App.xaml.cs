using System.Windows;
using NoticeApp.Wpf.Services;

namespace NoticeApp.Wpf;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) =>
        {
            ErrorLogger.Log(args.Exception, "App.DispatcherUnhandledException");
            System.Windows.MessageBox.Show(
                $"程序发生异常，已记录日志:\n{ErrorLogger.ErrorLogPath}\n\n{args.Exception.Message}",
                "Notice App",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                ErrorLogger.Log(exception, "AppDomain.CurrentDomain.UnhandledException");
            }
        };

        base.OnStartup(e);
    }
}
