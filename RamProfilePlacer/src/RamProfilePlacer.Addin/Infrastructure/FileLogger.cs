using System.IO;

namespace RamProfilePlacer.Addin.Infrastructure;

/// <summary>
/// Текстовый логгер. Файл: %AppData%\RamProfilePlacer\logs\profileplacer-YYYYMMDD.log
/// Формат строки: [HH:mm:ss.fff] [LEVEL] message
/// Потокобезопасен через lock.
/// </summary>
public sealed class FileLogger : IDisposable
{
    private static FileLogger? _instance;
    private static readonly object _instanceLock = new();

    private readonly string _logPath;
    private readonly object _writeLock = new();
    private bool _disposed;

    public static FileLogger Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    _instance ??= new FileLogger();
                }
            }
            return _instance;
        }
    }

    private FileLogger()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logsDir = Path.Combine(appData, "RamProfilePlacer", "logs");
        Directory.CreateDirectory(logsDir);
        _logPath = Path.Combine(logsDir, $"profileplacer-{DateTime.Now:yyyyMMdd}.log");
    }

    public void Info(string message) => Write("INFO", message);
    public void Warn(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    public void Error(string message, Exception ex) =>
        Write("ERROR", $"{message}{Environment.NewLine}{ex}");

    private void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] [{level}] {message}";
        lock (_writeLock)
        {
            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // Игнорируем ошибки логирования — они не должны ломать плагин
            }
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        lock (_instanceLock) { _instance = null; }
    }
}
