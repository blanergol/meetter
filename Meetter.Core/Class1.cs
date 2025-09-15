using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Meetter.Core;

public enum MeetingProviderType
{
	Unknown = 0,
	GoogleMeet = 1,
	Zoom = 2
}

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

public sealed class ProviderAccount
{
	public required string ProviderId { get; init; } // e.g. "google:accountEmail"
	public required string DisplayName { get; init; }
}

public interface ICalendarProvider
{
	string Id { get; }
	string DisplayName { get; }
	Task<IReadOnlyList<Meeting>> FetchMeetingsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public interface IMeetingLinkDetector
{
	MeetingProviderType ProviderType { get; }
	bool TryExtractJoinUrl(string text, out string? url);
}

public interface IMeetingsAggregator
{
	Task<IReadOnlyList<Meeting>> GetMeetingsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}

public sealed class MeetingsAggregator : IMeetingsAggregator
{
	private readonly IEnumerable<ICalendarProvider> _providers;

	public MeetingsAggregator(IEnumerable<ICalendarProvider> providers)
	{
		_providers = providers;
	}

	public async Task<IReadOnlyList<Meeting>> GetMeetingsAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
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
