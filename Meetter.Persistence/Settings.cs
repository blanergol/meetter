using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace Meetter.Persistence;

public sealed class EmailAccount
{
	public required string ProviderId { get; set; } // e.g. "google"
	public required string Email { get; set; }
	public string DisplayName { get; set; } = string.Empty;
	public bool Enabled { get; set; } = true;
	public Dictionary<string, string> Properties { get; set; } = new();
}

public sealed class AppSettings
{
	public int DaysToShow { get; set; } = 3; // 1..7
	public List<EmailAccount> Accounts { get; set; } = new();
    public int NotifyMinutes { get; set; } = 5; // минуты до встречи для уведомления
	public bool AutoStart { get; set; } = false; // запускать приложение при старте Windows
}

public interface ISettingsStore
{
	Task<AppSettings> LoadAsync();
	Task SaveAsync(AppSettings settings);
}

public sealed class JsonSettingsStore : ISettingsStore
{
	private readonly string _filePath;
	private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
	{
		WriteIndented = true
	};

	public JsonSettingsStore(string filePath)
	{
		_filePath = filePath;
	}

	public async Task<AppSettings> LoadAsync()
	{
		if (!File.Exists(_filePath))
		{
			return new AppSettings();
		}
		await using var stream = new FileStream(
			_filePath,
			FileMode.Open,
			FileAccess.Read,
			FileShare.ReadWrite | FileShare.Delete);
		var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options);
		return settings ?? new AppSettings();
	}

	public async Task SaveAsync(AppSettings settings)
	{
		var dir = Path.GetDirectoryName(_filePath);
		if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
		{
			Directory.CreateDirectory(dir);
		}
		var tempPath = _filePath + ".tmp";
		await using (var tmp = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
		{
			await JsonSerializer.SerializeAsync(tmp, settings, Options);
			await tmp.FlushAsync();
		}
		try
		{
			if (File.Exists(_filePath))
			{
				File.Replace(tempPath, _filePath, null);
			}
			else
			{
				File.Move(tempPath, _filePath);
			}
		}
		finally
		{
			if (File.Exists(tempPath))
			{
				try { File.Delete(tempPath); } catch { }
			}
		}
	}
}

