using System.Text;
using System.IO;

namespace NoticeApp.Wpf.Services;

public static class ErrorLogger
{
    public static string ErrorLogPath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var directory = Path.Combine(appData, "NoticeApp");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "error.log");
        }
    }

    public static void Log(Exception exception, string source)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine(new string('-', 64));
            sb.AppendLine($"Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
            sb.AppendLine($"Source: {source}");
            sb.AppendLine($"Message: {exception.Message}");
            sb.AppendLine("StackTrace:");
            sb.AppendLine(exception.ToString());
            File.AppendAllText(ErrorLogPath, sb.ToString(), Encoding.UTF8);
        }
        catch
        {
            // Ignore logging failure.
        }
    }
}
