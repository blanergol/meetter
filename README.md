# Meetter (.NET 9, WinForms + WPF)

Windows application that aggregates meetings from calendars (Google Calendar, others later), extracts Google Meet/Zoom links, and displays them in a convenient list.

## Projects
- `Meetter.WinForms` — main application (WinForms UI)
- `Meetter.App` — alternative WPF UI
- `Meetter.Core` — models/interfaces/link detectors
- `Meetter.Persistence` — settings and cache
- `Meetter.Providers.Google` — Google Calendar provider (OAuth)

## Requirements
- Windows 10/11
- .NET SDK 9.0 + Windows Desktop workload:
```powershell
dotnet workload install windowsdesktop
```
- Build and run from a regular path on the Windows drive (e.g., `C:\dev\meetter`), not from `\\wsl$`.

## Build and publish (WinForms)
```powershell
# In the solution folder
dotnet restore

# Publish as a single exe (win-x64)
dotnet publish Meetter.WinForms -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishReadyToRun=true -p:DebugType=none `
  -o C:\Apps\Meetter

# Run
C:\Apps\Meetter\Meetter.WinForms.exe
```

WPF publishing (if needed):
```powershell
dotnet publish Meetter.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishReadyToRun=true -p:DebugType=none `
  -o C:\Apps\Meetter.Wpf
```

## First run
1. Create an OAuth 2.0 Client ID (Desktop App) in Google Cloud and download `credentials.json`.
2. Place `credentials.json` next to the exe. If the file is missing, the app will prompt you to select it when adding an account.
3. Open Settings → Accounts tab → Add Google account. A browser will open for authorization.
4. The token will be saved to `%APPDATA%/Meetter/google/<email>`.

## Settings
File: `%APPDATA%/Meetter/settings.json`
```json
{
  "daysToShow": 3,
  "accounts": [
    {
      "providerId": "google",
      "email": "user@example.com",
      "displayName": "user@example.com",
      "enabled": true,
      "properties": {
        "credentialsPath": "credentials.json",
        "tokenPath": "%APPDATA%/Meetter/google/user@example.com"
      }
    }
  ]
}
```

## Meetings cache
- File cache for 1 hour: `%APPDATA%/Meetter/cache/meetings-YYYYMMDD-YYYYMMDD.json`
- Auto-load uses the cache; the Refresh button forces re-fetch and updates the cache.

## UI
- Main window: title and “Refresh” on one line (button on the right), below is the meeting list.
- List grouped by dates: “Today” for current, then dates. There is a loader during loading.
- Clicking a meeting opens the link in the browser/client.

## Troubleshooting
- For the “Calendar API disabled/Forbidden” error: enable Google Calendar API for the project your `credentials.json` belongs to, wait 2–5 minutes, and sign in again.
- To re-authorize, delete the token folder `%APPDATA%/Meetter/google/<email>` and add the account again.
- For the “file in use” error during publishing: close the running exe or publish to a new folder (`-o C:\Apps\Meetter-<timestamp>`).

## Extensibility
- New calendar providers: implement `ICalendarProvider` in a separate library.
- New meeting platforms: implement `IMeetingLinkDetector` (Regex detectors already for Meet/Zoom, can be extended).
