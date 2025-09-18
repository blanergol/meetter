using Meetter.Core;
using Meetter.Persistence;
using Meetter.Providers.Google;
using System.Diagnostics;

namespace Meetter.App;

public sealed class MainForm : Form
{
    private readonly ISettingsStore _settingsStore;
    private readonly MenuStrip _menu;
    private readonly ListView _list;
    private readonly Button _refresh;
    private readonly ProgressBar _loader;
    private readonly Panel _header;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private IReadOnlyList<Meeting> _lastMeetings = Array.Empty<Meeting>();
    private readonly HashSet<string> _notifiedMeetings = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> _startedNotified = new HashSet<string>(StringComparer.Ordinal);
    private DateTimeOffset _lastRefreshUtc = DateTimeOffset.MinValue;
    private ListViewItem? _hoverItem;
    private static readonly Color HoverBackColor = Color.FromArgb(0xF2, 0xF6, 0xFC);
    private AppSettings? _currentSettings;

    public MainForm()
    {
        Text = "Meetter";
        Width = 1050;
        Height = 700;
        Icon = AppIconFactory.CreateIcon();
        try
        {
            AppLogger.Info("MainForm ctor");
        }
        catch
        {
        }

        _settingsStore = new JsonSettingsStore(PathHelper.GetSettingsPath());

        // Menu
        _menu = new MenuStrip { Dock = DockStyle.Top };
        var file = new ToolStripMenuItem("File");
        var settingsItem = new ToolStripMenuItem("Settings", null, (_, __) =>
        {
            using var f = new SettingsForm(_settingsStore);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                _ = LoadMeetingsAsync(false);
            }
        });
        var exitItem = new ToolStripMenuItem("Exit", null, (_, __) => Close());
        file.DropDownItems.Add(settingsItem);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(exitItem);

        var help = new ToolStripMenuItem("Help");
        var logItem = new ToolStripMenuItem("Logging", null, (_, __) =>
        {
            try
            {
                using var f = new LogViewerForm();
                f.ShowDialog(this);
            }
            catch
            {
            }
        });
        var aboutItem = new ToolStripMenuItem("About", null, (_, __) =>
        {
            using var f = new AboutForm();
            f.ShowDialog(this);
        });
        help.DropDownItems.Add(logItem);
        help.DropDownItems.Add(aboutItem);
        _menu.Items.Add(file);
        _menu.Items.Add(help);
        MainMenuStrip = _menu;

        // Header panel (flow to keep controls visible on high DPI)
        _header = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(10, 8, 10, 8), FlowDirection = FlowDirection.RightToLeft, WrapContents = false
        };
        _refresh = new Button { Text = "Refresh", AutoSize = true, Margin = new Padding(8, 0, 0, 0) };
        _refresh.Click += async (_, __) =>
        {
            try
            {
                AppLogger.Debug("Manual refresh clicked");
            }
            catch
            {
            }

            await LoadMeetingsAsync(false);
        };
        _loader = new ProgressBar
            { Style = ProgressBarStyle.Marquee, Width = 200, Visible = false, Margin = new Padding(8, 3, 8, 3) };
        _header.Controls.Add(_refresh);
        _header.Controls.Add(_loader);

        // List
        _list = new SmoothListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
        _list.Columns.Add("Time", 230);
        _list.Columns.Add("Title", 600);
        _list.Columns.Add("Provider", 140);
        _list.MouseMove += OnListMouseMove;
        _list.MouseLeave += OnListMouseLeave;

        Controls.Add(_list);
        Controls.Add(_header);
        Controls.Add(_menu);
        _ = LoadMeetingsAsync(true);
        _list.DoubleClick += (_, __) =>
        {
            if (_list.SelectedItems.Count == 1 && _list.SelectedItems[0].Tag is string url &&
                !string.IsNullOrWhiteSpace(url))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    AppLogger.Info($"Open link: {url}");
                }
                catch (Exception ex)
                {
                    try
                    {
                        AppLogger.Error("Open link failed", ex);
                    }
                    catch
                    {
                    }
                }
            }
        };

        // Tray icon
        _tray = new NotifyIcon
        {
            Text = "Meetter",
            Icon = this.Icon,
            Visible = true
        };
        _tray.DoubleClick += (_, __) =>
        {
            Show();
            ShowInTaskbar = true;
            WindowState = FormWindowState.Normal;
            Activate();
        };
        FormClosing += (s, e) =>
        {
            e.Cancel = true;
            Hide();
            try
            {
                AppLogger.Info("MainForm hide to tray");
            }
            catch
            {
            }
        };

        // Polling timer for notifications and tray updates
        _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _timer.Tick += async (_, __) =>
        {
            try
            {
                AppLogger.Debug("Timer tick");
            }
            catch
            {
            }

            await CheckNotificationsAsync();
            UpdateTrayRemaining(DateTimeOffset.Now);
            BuildTrayMenu(DateTimeOffset.Now);
            // Periodic background refresh (without cache) to include newly added meetings
            if (DateTimeOffset.UtcNow - _lastRefreshUtc > TimeSpan.FromMinutes(5))
            {
                await LoadMeetingsAsync(false);
            }
        };
        _timer.Start();
    }

    private async Task LoadMeetingsAsync(bool useCache)
    {
        try
        {
            try
            {
                AppLogger.Info($"LoadMeetings start (useCache={useCache})");
            }
            catch
            {
            }

            _loader.Visible = true;
            _refresh.Enabled = false;
            var settings = await _settingsStore.LoadAsync();
            _currentSettings = settings;
            var detectors = new IMeetingLinkDetector[] { new GoogleMeetLinkDetector(), new ZoomLinkDetector() };
            var providers = new List<ICalendarProvider>();
            foreach (var acc in settings.Accounts.Where(a => a.Enabled))
            {
                if (acc.ProviderId == GoogleCalendarProvider.ProviderKey)
                {
                    var token = acc.Properties.TryGetValue("tokenPath", out var tp)
                        ? tp
                        : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                            "Meetter", "google", acc.Email);
                    providers.Add(new GoogleCalendarProvider(detectors, "Meetter", token));
                }
            }

            var aggregator = new MeetingsAggregator(providers);
            var from = DateTimeOffset.Now.Date;
            var to = from.AddDays(Math.Clamp(settings.DaysToShow, 1, 7));
            var cache = new FileMeetingsCache();
            IReadOnlyList<Meeting> items;
            if (useCache)
            {
                var (hit, cached) = await cache.TryReadAsync(from, to);
                if (hit)
                {
                    items = cached;
                    try
                    {
                        AppLogger.Debug($"Cache hit: {cached.Count} items");
                    }
                    catch
                    {
                    }
                }
                else
                {
                    items = await aggregator.GetMeetingsAsync(from, to, CancellationToken.None);
                    try
                    {
                        AppLogger.Debug($"Fetched: {items.Count} items");
                    }
                    catch
                    {
                    }

                    await cache.WriteAsync(from, to, items);
                }
            }
            else
            {
                items = await aggregator.GetMeetingsAsync(from, to, CancellationToken.None);
                try
                {
                    AppLogger.Debug($"Fetched (no cache): {items.Count} items");
                }
                catch
                {
                }

                await cache.WriteAsync(from, to, items);
            }

            BindMeetings(items);
            _lastRefreshUtc = DateTimeOffset.UtcNow;
        }
        catch (Exception ex)
        {
            try
            {
                AppLogger.Error("LoadMeetings failed", ex);
            }
            catch
            {
            }

            MessageBox.Show(ex.Message, "Load error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            try
            {
                AppLogger.Info("LoadMeetings end");
            }
            catch
            {
            }

            _loader.Visible = false;
            _refresh.Enabled = true;
        }
    }

    private void BindMeetings(IReadOnlyList<Meeting> items)
    {
        _lastMeetings = items;
        _list.BeginUpdate();
        _list.Items.Clear();
        _hoverItem = null;
        // Group by dates — insert visual header rows
        var now = DateTimeOffset.Now;
        var grace = TimeSpan.FromMinutes(Math.Max(0, _currentSettings?.MinutesShowAfterStart ?? 5));
        var groups = items.GroupBy(m => m.StartTime.Date).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            List<Meeting> dayMeetings;
            if (g.Key == now.Date)
            {
                dayMeetings = g.Where(m => ShouldShowToday(m, now, grace)).ToList();
            }
            else
            {
                dayMeetings = g.ToList();
            }
            if (dayMeetings.Count == 0) continue;
            var header = g.Key == now.Date ? "Today" : g.Key.ToString("dddd, dd.MM.yyyy");
            var groupItem = new ListViewItem(new[] { header, "", "" })
                { BackColor = System.Drawing.Color.FromArgb(0xEF, 0xF5, 0xFF) };
            groupItem.Font = new Font(_list.Font, FontStyle.Bold);
            _list.Items.Add(groupItem);
            foreach (var m in dayMeetings)
            {
                var li = new ListViewItem(new[] { m.StartTime.ToString("HH:mm"), m.Title, m.ProviderType.ToString() })
                    { Tag = m.JoinUrl };
                _list.Items.Add(li);
            }
        }

        _list.EndUpdate();

        // Build tray menu and initial tooltip
        BuildTrayMenu(now);
        UpdateTrayRemaining(now);
    }

    private void OnListMouseMove(object? sender, MouseEventArgs e)
    {
        var hit = _list.HitTest(e.Location);
        var item = hit.Item;
        if (item == null)
        {
            ClearHover();
            return;
        }

        if (ReferenceEquals(item, _hoverItem)) return;
        ClearHover();
        if (IsGroupHeader(item)) return;
        _hoverItem = item;
        _hoverItem.BackColor = HoverBackColor;
    }

    private void OnListMouseLeave(object? sender, EventArgs e)
    {
        ClearHover();
    }

    private void ClearHover()
    {
        if (_hoverItem != null)
        {
            // Сбросить цвет к стандартному
            _hoverItem.BackColor = _list.BackColor;
            _hoverItem = null;
        }
    }

    private static bool IsGroupHeader(ListViewItem item)
    {
        // У нас заголовки суток создаются с пустыми колонками 2 и 3, и жирным шрифтом
        return string.IsNullOrEmpty(item.SubItems.Count > 1 ? item.SubItems[1].Text : null) &&
               string.IsNullOrEmpty(item.SubItems.Count > 2 ? item.SubItems[2].Text : null) &&
               item.Font.Bold;
    }

    private async Task CheckNotificationsAsync()
    {
        try
        {
            var settings = await _settingsStore.LoadAsync();
            var notifyIn = TimeSpan.FromMinutes(Math.Max(0, settings.NotifyMinutes));
            var now = DateTimeOffset.Now;
            foreach (var m in _lastMeetings)
            {
                if (string.IsNullOrWhiteSpace(m.JoinUrl)) continue;
                var delta = m.StartTime - now;
                var key = $"{m.Title}|{m.StartTime.ToUnixTimeSeconds()}";
                if (_notifiedMeetings.Contains(key)) continue;
                if (delta <= notifyIn && delta > TimeSpan.Zero)
                {
                    // show toast
                    ShowToast($"Upcoming meeting: {m.Title}", m.JoinUrl!);
                    _notifiedMeetings.Add(key);
                }

                // Notify exactly at start (once)
                if (!_startedNotified.Contains(key) && delta <= TimeSpan.Zero && delta > TimeSpan.FromMinutes(-1))
                {
                    ShowToast($"Meeting started: {m.Title}", m.JoinUrl!);
                    _startedNotified.Add(key);
                }
            }
        }
        catch
        {
        }
    }

    private void ShowToast(string title, string url)
    {
        // Fallback via balloon tip + quick click to join
        _tray.BalloonTipTitle = title.Length > 60 ? title.Substring(0, 60) + "…" : title;
        _tray.BalloonTipText = "Click to join";
        EventHandler? handler = null;
        handler = (_, __) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
            }

            _tray.BalloonTipClicked -= handler;
        };
        _tray.BalloonTipClicked += handler;
        _tray.ShowBalloonTip(5000);
    }

    private static string FormatRemaining(TimeSpan delta)
    {
        if (delta.TotalSeconds < 1) return "0 min";
        var days = (int)delta.TotalDays;
        var hours = delta.Hours;
        var minutes = delta.Minutes;
        if (days > 0) return hours > 0 ? $"{days} d {hours} h" : $"{days} d";
        if (hours > 0) return minutes > 0 ? $"{hours} h {minutes} min" : $"{hours} h";
        return minutes <= 0 ? "< 1 min" : $"{minutes} min";
    }

    private void BuildTrayMenu(DateTimeOffset now)
    {
        var menu = new ContextMenuStrip();
        var today = now.Date;
        var grace = TimeSpan.FromMinutes(Math.Max(0, _currentSettings?.MinutesShowAfterStart ?? 5));
        var todayVisible = _lastMeetings
            .Where(m => m.StartTime.Date == today && ShouldShowToday(m, now, grace))
            .OrderBy(m => m.StartTime)
            .ToList();
        var highlight = todayVisible.FirstOrDefault();
        foreach (var m in todayVisible)
        {
            var title = m.Title.Length > 30 ? m.Title.Substring(0, 30) + "…" : m.Title;
            var isOngoing = IsOngoingForLabel(m, now, grace);
            var isHighlight = highlight != null && ReferenceEquals(m, highlight);
            string? right = null;
            if (isHighlight)
            {
                right = isOngoing ? "Уже идет" : FormatRemaining(m.StartTime - now);
            }
            var display = right != null ? $"{title} ({right})" : title;
            var mi = new ToolStripMenuItem(display) { ToolTipText = m.Title };
            var url = m.JoinUrl;
            mi.Click += (_, __) =>
            {
                if (!string.IsNullOrWhiteSpace(url))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    catch
                    {
                    }
                }
            };
            if (right != null)
            {
                mi.Tag = new BoldParts { Left = title + " ", Right = "(" + right + ")" };
            }

            menu.Items.Add(mi);
        }

        if (menu.Items.Count > 0) menu.Items.Add(new ToolStripSeparator());
        var about = new ToolStripMenuItem("About", null, (_, __) =>
        {
            using var f = new AboutForm();
            f.ShowDialog(this);
        });
        var settings = new ToolStripMenuItem("Settings", null, async (_, __) =>
        {
            using var f = new SettingsForm(_settingsStore);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                await LoadMeetingsAsync(false);
            }
        });
        var exit = new ToolStripMenuItem("Exit", null, (_, __) =>
        {
            try
            {
                _tray.Visible = false;
            }
            catch
            {
            }

            Application.Exit();
        });
        menu.Items.Add(settings);
        menu.Items.Add(about);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        menu.Renderer = new BoldParenRenderer();
        menu.Opening += (_, __) => { BuildTrayMenu(DateTimeOffset.Now); };
        _tray.ContextMenuStrip = menu;
    }

    private void UpdateTrayRemaining(DateTimeOffset now)
    {
        try
        {
            var grace = TimeSpan.FromMinutes(Math.Max(0, _currentSettings?.MinutesShowAfterStart ?? 5));
            var ongoing = _lastMeetings.FirstOrDefault(m => IsOngoingForLabel(m, now, grace));
            if (ongoing != null)
            {
                _tray.Text = "Meetter — Уже идет";
            }
            else
            {
                var upcoming = _lastMeetings.Where(m => m.StartTime > now).OrderBy(m => m.StartTime)
                    .FirstOrDefault();
                if (upcoming != null)
                {
                    var remaining = FormatRemaining(upcoming.StartTime - now);
                    _tray.Text = $"Meetter — через {remaining} до встречи";
                }
                else
                {
                    _tray.Text = "Meetter";
                }
            }
        }
        catch
        {
            _tray.Text = "Meetter";
        }
    }

    private static bool ShouldShowToday(Meeting m, DateTimeOffset now, TimeSpan grace)
    {
        if (m.StartTime >= now) return true;
        if (m.EndTime.HasValue && m.EndTime.Value >= now) return true;
        if (!m.EndTime.HasValue && now >= m.StartTime && now - m.StartTime <= grace) return true;
        return false;
    }

    private static bool IsOngoingForLabel(Meeting m, DateTimeOffset now, TimeSpan grace)
    {
        if (now < m.StartTime) return false;
        if (m.EndTime.HasValue) return now <= m.EndTime.Value;
        return now - m.StartTime <= grace;
    }

    private sealed class BoldParenRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            if (e.Item is not ToolStripMenuItem mi || mi.Tag is not BoldParts parts)
            {
                base.OnRenderItemText(e);
                return;
            }

            var left = parts.Left ?? string.Empty;
            var right = parts.Right ?? string.Empty;
            var flags = TextFormatFlags.NoPrefix | TextFormatFlags.NoPadding;

            // base color respects selection highlight
            var color = e.TextColor;
            var x = e.TextRectangle.Left;
            var y = e.TextRectangle.Top + (e.TextRectangle.Height - e.TextFont.Height) / 2;
            var leftSize = TextRenderer.MeasureText(e.Graphics, left, e.TextFont, Size.Empty, flags);
            TextRenderer.DrawText(e.Graphics, left, e.TextFont, new Point(x, y), color, flags);
            x += leftSize.Width;
            using var bold = e.TextFont.Style.HasFlag(FontStyle.Bold)
                ? e.TextFont
                : new Font(e.TextFont, FontStyle.Bold);
            var y2 = e.TextRectangle.Top + (e.TextRectangle.Height - bold.Height) / 2;
            TextRenderer.DrawText(e.Graphics, right, bold, new Point(x, y2), color, flags);
        }
    }

    private sealed class BoldParts
    {
        public string? Left { get; set; }
        public string? Right { get; set; }
    }
}

internal sealed class SmoothListView : ListView
{
    public SmoothListView()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw,
            true);
        UpdateStyles();
    }
}

internal static class PathHelper
{
    public static string GetSettingsPath() =>
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter",
            "settings.json");
}