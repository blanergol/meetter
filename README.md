# Meetter (.NET 9, WinForms)

Windows application that aggregates meetings from calendars (Google Calendar, others later), extracts Google Meet/Zoom links, and displays them in a convenient list.

## Projects
- `Meetter.App` — main application (WinForms UI)
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

## Build and publish
```powershell
# In the solution folder
dotnet restore

# Publish as a single exe (win-x64)
dotnet publish Meetter.App -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:PublishReadyToRun=true -p:DebugType=none `
  -o C:\Apps\Meetter

# Run
C:\Apps\Meetter\Meetter.App.exe
```

## Google OAuth secrets
Build now generates `GoogleSecrets.g.cs` from environment variables. Set them before build/publish:

```bash
export GOOGLE_CLIENT_ID="<your_client_id>"
export GOOGLE_CLIENT_SECRET="<your_client_secret>"
```

Then build/publish as usual:

```bash
dotnet build
dotnet publish Meetter.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

If variables are not set, values will be empty and authorization will fail.

## First run
1. Open Settings → Accounts tab → Add Google account. A browser will open for authorization (OAuth Desktop + PKCE).
2. The refresh token will be saved to `%APPDATA%/Meetter/google/<email>` and used automatically next time.

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

## Microsoft Store (MSIX) autostart
- Unpackaged EXE uses registry key `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Packaged (MSIX/Store) app uses `Windows.ApplicationModel.StartupTask` and requires `windows.startupTask` in `Package.appxmanifest`.
- Task id in manifest must be exactly `MeetterStartupTask`.

Manifest snippet:
```xml
<Applications>
  <Application ...>
    <Extensions>
      <desktop:Extension Category="windows.startupTask" Executable="Meetter.exe" EntryPoint="Windows.FullTrustApplication">
        <desktop:StartupTask TaskId="MeetterStartupTask" Enabled="false" DisplayName="Meetter" />
      </desktop:Extension>
    </Extensions>
  </Application>
</Applications>
```

## Troubleshooting
- For the “Calendar API disabled/Forbidden” error: enable Google Calendar API for the GCP project used by the app, wait 2–5 minutes, and sign in again.
- To re-authorize, delete the token folder `%APPDATA%/Meetter/google/<email>` and add the account again.
- For the “file in use” error during publishing: close the running exe or publish to a new folder (`-o C:\Apps\Meetter-<timestamp>`).

## Extensibility
- New calendar providers: implement `ICalendarProvider` in a separate library.
- New meeting platforms: implement `IMeetingLinkDetector` (Regex detectors already for Meet/Zoom, can be extended).
