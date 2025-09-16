using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Meetter.Core;

namespace Meetter.Providers.Google;

public sealed class GoogleCalendarProvider : ICalendarProvider
{
    public const string ProviderKey = "google";
    private readonly IMeetingLinkDetector[] _detectors;
    private readonly string _appName;
    private readonly string _credentialsPath;
    private readonly string _tokenPath;

    public GoogleCalendarProvider(IMeetingLinkDetector[] detectors, string appName, string credentialsPath,
        string tokenPath)
    {
        _detectors = detectors;
        _appName = appName;
        _credentialsPath = credentialsPath;
        _tokenPath = tokenPath;
    }

    public string Id => ProviderKey;
    public string DisplayName => "Google Calendar";

    public async Task<IReadOnlyList<Meeting>> FetchMeetingsAsync(DateTimeOffset from, DateTimeOffset to,
        CancellationToken cancellationToken)
    {
        UserCredential credential;
        ClientSecrets secrets = GoogleOAuthConfig.GetClientSecrets();
        credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets, new[] { CalendarService.Scope.CalendarReadonly }, "user", cancellationToken,
            new FileDataStore(_tokenPath, true));

        using var service = new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = _appName,
        });

        var list = new List<Meeting>();
        Logger.Info($"Google: start fetch tokenPath={_tokenPath}");
        // Collect all user calendars
        var calendars = await service.CalendarList.List().ExecuteAsync(cancellationToken);
        var calendarIds = (calendars.Items ?? new List<CalendarListEntry>()).Select(c => c.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        Logger.Info($"Google: calendars={calendarIds.Count}");
        foreach (var calId in calendarIds)
        {
            Logger.Info($"Google: list events cal={calId}");
            var eventsRequest = service.Events.List(calId);
            eventsRequest.TimeMinDateTimeOffset = from;
            eventsRequest.TimeMaxDateTimeOffset = to;
            eventsRequest.ShowDeleted = false;
            eventsRequest.SingleEvents = true;
            eventsRequest.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
            var events = await eventsRequest.ExecuteAsync(cancellationToken);
            Logger.Info($"Google: events count={(events.Items?.Count ?? 0)} cal={calId}");
            if (events.Items == null) continue;
            foreach (var ev in events.Items)
            {
                DateTimeOffset? start = ev.Start?.DateTimeDateTimeOffset ?? (!string.IsNullOrEmpty(ev.Start?.Date)
                    ? DateTimeOffset.Parse(ev.Start!.Date)
                    : (DateTimeOffset?)null);
                DateTimeOffset? end = ev.End?.DateTimeDateTimeOffset ?? (!string.IsNullOrEmpty(ev.End?.Date)
                    ? DateTimeOffset.Parse(ev.End!.Date)
                    : (DateTimeOffset?)null);
                var text = string.Join("\n",
                    new[] { ev.Summary, ev.Description, ev.Location, ev.HangoutLink }
                        .Where(s => !string.IsNullOrWhiteSpace(s))!);

                // conference entry points
                if (ev.ConferenceData?.EntryPoints != null)
                {
                    foreach (var ep in ev.ConferenceData.EntryPoints)
                    {
                        if (!string.IsNullOrWhiteSpace(ep.Uri))
                            text += "\n" + ep.Uri;
                    }
                }

                // attachments
                if (ev.Attachments != null)
                {
                    foreach (var at in ev.Attachments)
                    {
                        if (!string.IsNullOrWhiteSpace(at.FileUrl)) text += "\n" + at.FileUrl;
                        if (!string.IsNullOrWhiteSpace(at.Title)) text += "\n" + at.Title;
                    }
                }

                string? joinUrl = null;
                MeetingProviderType providerType = MeetingProviderType.Unknown;
                foreach (var d in _detectors)
                {
                    if (d.TryExtractJoinUrl(text ?? string.Empty, out var url))
                    {
                        joinUrl = url;
                        providerType = d.ProviderType;
                        break;
                    }
                }

                if (start is null) continue;
                if (string.IsNullOrWhiteSpace(joinUrl))
                {
                    Logger.Info($"Google: skip event without link id={ev.Id} cal={calId}");
                    // if no link found — skip so main list shows only meetings with providers
                    continue;
                }

                list.Add(new Meeting
                {
                    Id = ev.Id ?? Guid.NewGuid().ToString("N"),
                    Title = string.IsNullOrWhiteSpace(ev.Summary) ? "(untitled)" : ev.Summary,
                    StartTime = start.Value,
                    EndTime = end,
                    JoinUrl = joinUrl,
                    ProviderType = providerType,
                    CalendarId = calId
                });
            }
        }

        return list;
    }
}