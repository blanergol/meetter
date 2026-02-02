using System.Diagnostics.CodeAnalysis;

namespace Meetter.Core;

public enum MeetingProviderType
{
    Unknown = 0,
    GoogleMeet = 1,
    Zoom = 2,
    RedMadRobotMeet = 3
}

[SuppressMessage("ReSharper", "UnusedMember.Global")]
[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
public sealed class Meeting
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public DateTimeOffset StartTime { get; init; }
    public DateTimeOffset? EndTime { get; init; }
    public MeetingProviderType ProviderType { get; init; }
    public string? JoinUrl { get; init; }
    public string? CalendarId { get; init; }
}

public interface ICalendarProvider
{
    Task<IReadOnlyList<Meeting>> FetchMeetingsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken);
}

public interface IMeetingLinkDetector
{
    MeetingProviderType ProviderType { get; }
    bool TryExtractJoinUrl(string text, out string? url);
}

public interface IMeetingsAggregator
{
    Task<IReadOnlyList<Meeting>> GetMeetingsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken);
}

public sealed class MeetingsAggregator : IMeetingsAggregator
{
    private readonly IEnumerable<ICalendarProvider> _providers;

    public MeetingsAggregator(IEnumerable<ICalendarProvider> providers)
    {
        _providers = providers;
    }

    public async Task<IReadOnlyList<Meeting>> GetMeetingsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        var result = new List<Meeting>();
        foreach (var provider in _providers)
        {
            var meetings = await provider.FetchMeetingsAsync(from, to, cancellationToken).ConfigureAwait(false);
            result.AddRange(meetings);
        }

        result.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        return result;
    }
}

