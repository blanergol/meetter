namespace Meetter.Core;

public static class Logger
{
    private static Action<string>? _info;
    private static Action<string, Exception?>? _error;

    public static void Initialize(Action<string>? info = null, Action<string, Exception?>? error = null)
    {
        _info = info;
        _error = error;
    }

    public static void Info(string message) => _info?.Invoke(message);
    public static void Error(string message, Exception? ex = null) => _error?.Invoke(message, ex);
}