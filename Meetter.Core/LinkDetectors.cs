using System.Text.RegularExpressions;
using System.Net;

namespace Meetter.Core;

internal static class LinkDetectorHelpers
{
    private static readonly Regex GoogleRedirect = new(@"https?:\/\/www\.google\.com\/url\?q=([^&\s]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text;
        foreach (Match m in GoogleRedirect.Matches(text))
        {
            if (m.Groups.Count > 1)
            {
                var encoded = m.Groups[1].Value;
                var decoded = WebUtility.UrlDecode(encoded);
                if (!string.IsNullOrWhiteSpace(decoded))
                    normalized += "\n" + decoded;
            }
        }

        return normalized;
    }
}

public sealed class GoogleMeetLinkDetector : IMeetingLinkDetector
{
    private static readonly Regex MeetRegex =
        new(@"https?:\/\/meet\.google\.com\/(?:lookup\/)?[a-z0-9\-]+(?:\?[\w\-=&%]+)?(?:#[\w\-=&%]+)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MeetingProviderType ProviderType => MeetingProviderType.GoogleMeet;

    public bool TryExtractJoinUrl(string text, out string? url)
    {
        text = LinkDetectorHelpers.NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            url = null;
            return false;
        }

        var match = MeetRegex.Match(text);
        if (match.Success)
        {
            url = match.Value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? match.Value
                : "https://" + match.Value;
            return true;
        }

        url = null;
        return false;
    }
}

public sealed class ZoomLinkDetector : IMeetingLinkDetector
{
    private static readonly Regex ZoomRegex = new(@"https?:\/\/(?:[\w\.-]+\.)?zoom\.us\/(?:j|my|wc\/join)\/[^\s]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MeetingProviderType ProviderType => MeetingProviderType.Zoom;

    public bool TryExtractJoinUrl(string text, out string? url)
    {
        text = LinkDetectorHelpers.NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            url = null;
            return false;
        }

        var match = ZoomRegex.Match(text);
        if (match.Success)
        {
            url = match.Value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? match.Value
                : "https://" + match.Value;
            return true;
        }

        url = null;
        return false;
    }
}

public sealed class RedMadRobotMeetLinkDetector : IMeetingLinkDetector
{
    private static readonly Regex RedMadRobotRegex =
        new(@"https?:\/\/meet\.redmadrobot\.com\/[^\s]+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public MeetingProviderType ProviderType => MeetingProviderType.RedMadRobotMeet;

    public bool TryExtractJoinUrl(string text, out string? url)
    {
        text = LinkDetectorHelpers.NormalizeText(text);
        if (string.IsNullOrWhiteSpace(text))
        {
            url = null;
            return false;
        }

        var match = RedMadRobotRegex.Match(text);
        if (match.Success)
        {
            url = match.Value.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? match.Value
                : "https://" + match.Value;
            return true;
        }

        url = null;
        return false;
    }
}
