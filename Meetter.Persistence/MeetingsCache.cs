using Meetter.Core;
using System.Text.Json;

namespace Meetter.Persistence;

public interface IMeetingsCache
{
    Task<(bool isHit, IReadOnlyList<Meeting> meetings)> TryReadAsync(DateTimeOffset from, DateTimeOffset to);
    Task WriteAsync(DateTimeOffset from, DateTimeOffset to, IReadOnlyList<Meeting> meetings);
}

public sealed class FileMeetingsCache : IMeetingsCache
{
    private readonly string _cacheDir;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    public FileMeetingsCache(string? cacheDir = null)
    {
        _cacheDir = cacheDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Meetter", "cache");
    }

    public async Task<(bool isHit, IReadOnlyList<Meeting> meetings)> TryReadAsync(DateTimeOffset from,
        DateTimeOffset to)
    {
        var path = GetPath(from, to);
        if (!File.Exists(path)) return (false, Array.Empty<Meeting>());
        var info = new FileInfo(path);
        if (DateTimeOffset.Now - info.LastWriteTimeUtc > TimeSpan.FromHours(1)) return (false, Array.Empty<Meeting>());
        await using var stream = File.OpenRead(path);
        var data = await JsonSerializer.DeserializeAsync<List<Meeting>>(stream, Options) ?? new List<Meeting>();
        return (true, data);
    }

    public async Task WriteAsync(DateTimeOffset from, DateTimeOffset to, IReadOnlyList<Meeting> meetings)
    {
        var path = GetPath(from, to);
        var dir = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(dir);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, meetings, Options);
    }

    private string GetPath(DateTimeOffset from, DateTimeOffset to)
    {
        var key = $"{from.UtcDateTime:yyyyMMdd}-{to.UtcDateTime:yyyyMMdd}";
        return Path.Combine(_cacheDir, $"meetings-{key}.json");
    }
}


