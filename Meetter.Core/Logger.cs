using System;
using System.IO;

namespace Meetter.Core;

public static class Logger
{
	// No-op logger: отключено файловое логирование
	public static void Initialize(string? logFilePath = null) { }
	public static void Info(string message) { }
	public static void Error(string message, Exception? ex = null) { }
}

