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
		await using var stream = File.OpenRead(_filePath);
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
		await using var stream = File.Create(_filePath);
		await JsonSerializer.SerializeAsync(stream, settings, Options);
	}
}

