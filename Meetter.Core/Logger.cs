using System;
using System.IO;

namespace Meetter.Core;

public static class Logger
{
	// No-op logger: file logging is disabled
	public static void Initialize(string? logFilePath = null) { }
	public static void Info(string message) { }
	public static void Error(string message, Exception? ex = null) { }
}

