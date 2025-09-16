using Meetter.Core;
using Meetter.Persistence;
using Meetter.Providers.Google;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing;

namespace Meetter.WinForms;

public sealed class MainForm : Form
{
    private readonly ISettingsStore _settingsStore;
    private readonly MenuStrip _menu;
    private readonly ListView _list;
    private readonly Button _refresh;
    private readonly Label _title;
    private readonly ProgressBar _loader;
    private readonly Panel _header;
    private readonly NotifyIcon _tray;
    private readonly System.Windows.Forms.Timer _timer;
    private IReadOnlyList<Meeting> _lastMeetings = Array.Empty<Meeting>();

    public MainForm()
    {
        Text = "Meetter";
        Width = 900;
        Height = 600;
        Icon = AppIconFactory.CreateIcon();
        _settingsStore = new JsonSettingsStore(PathHelper.GetSettingsPath());

        // Menu
        _menu = new MenuStrip { Dock = DockStyle.Top };
        var file = new ToolStripMenuItem("Файл");
        var settingsItem = new ToolStripMenuItem("Настройки", null, (_, __) =>
        {
            using var f = new SettingsForm(_settingsStore);
            if (f.ShowDialog(this) == DialogResult.OK)
            {
                _ = LoadMeetingsAsync(false);
            }
        });
        var exitItem = new ToolStripMenuItem("Выход", null, (_, __) => Close());
        file.DropDownItems.Add(settingsItem);
        file.DropDownItems.Add(new ToolStripSeparator());
        file.DropDownItems.Add(exitItem);

        var help = new ToolStripMenuItem("Справка");
        var aboutItem = new ToolStripMenuItem("О программе", null, (_, __) => { using var f = new AboutForm(); f.ShowDialog(this); });
        help.DropDownItems.Add(aboutItem);
        _menu.Items.Add(file);
        _menu.Items.Add(help);
        MainMenuStrip = _menu;

        // Header panel
        _header = new Panel { Dock = DockStyle.Top, Height = 48, Padding = new Padding(10, 8, 10, 8) };
        _title = new Label { Text = "Ближайшие встречи", AutoSize = true, Dock = DockStyle.Left, Font = new System.Drawing.Font("Segoe UI", 12, System.Drawing.FontStyle.Bold) };
        _refresh = new Button { Text = "Обновить", Width = 120, Dock = DockStyle.Right };
        _refresh.Click += async (_, __) => await LoadMeetingsAsync(false);
        _loader = new ProgressBar { Style = ProgressBarStyle.Marquee, Width = 200, Dock = DockStyle.Right, Visible = false, Margin = new Padding(8, 0, 8, 0) };
        _header.Controls.Add(_refresh);
        _header.Controls.Add(_loader);
        _header.Controls.Add(_title);

        // List
        _list = new ListView { Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true };
        _list.Columns.Add("Время", 160);
        _list.Columns.Add("Название", 500);
        _list.Columns.Add("Провайдер", 140);

        Controls.Add(_list);
        Controls.Add(_header);
        Controls.Add(_menu);
        Shown += async (_, __) => await LoadMeetingsAsync(true);
        _list.DoubleClick += (_, __) =>
        {
            if (_list.SelectedItems.Count == 1 && _list.SelectedItems[0].Tag is string url && !string.IsNullOrWhiteSpace(url))
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            }
        };

        // Tray icon
        _tray = new NotifyIcon
        {
            Text = "Meetter",
            Icon = this.Icon,
            Visible = true
        };
        _tray.DoubleClick += (_, __) => { Show(); WindowState = FormWindowState.Normal; Activate(); };
        FormClosing += (s, e) => { e.Cancel = true; Hide(); };

        // Polling timer for notifications
        _timer = new System.Windows.Forms.Timer { Interval = 60_000 };
        _timer.Tick += async (_, __) => await CheckNotificationsAsync();
        _timer.Start();
    }

    private async Task LoadMeetingsAsync(bool useCache)
    {
        try
        {
            _loader.Visible = true;
            _refresh.Enabled = false;
            var settings = await _settingsStore.LoadAsync();
            var detectors = new IMeetingLinkDetector[] { new GoogleMeetLinkDetector(), new ZoomLinkDetector() };
            var providers = new List<ICalendarProvider>();
            foreach (var acc in settings.Accounts.Where(a => a.Enabled))
            {
                if (acc.ProviderId == GoogleCalendarProvider.ProviderKey)
                {
                    var creds = acc.Properties.TryGetValue("credentialsPath", out var cp) ? cp : "credentials.json";
                    var token = acc.Properties.TryGetValue("tokenPath", out var tp) ? tp : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "google", acc.Email);
                    providers.Add(new GoogleCalendarProvider(detectors, "Meetter", creds, token));
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
                if (hit) { items = cached; }
                else { items = await aggregator.GetMeetingsAsync(from, to, CancellationToken.None); await cache.WriteAsync(from, to, items); }
            }
            else
            {
                items = await aggregator.GetMeetingsAsync(from, to, CancellationToken.None);
                await cache.WriteAsync(from, to, items);
            }

            BindMeetings(items);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Ошибка загрузки", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _loader.Visible = false;
            _refresh.Enabled = true;
        }
    }

    private void BindMeetings(IReadOnlyList<Meeting> items)
    {
        _lastMeetings = items;
        _list.BeginUpdate();
        _list.Items.Clear();
        // группировка по дням — визуально вставим заголовки-строки
        var now = DateTimeOffset.Now;
        var groups = items.GroupBy(m => m.StartTime.Date).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            var dayMeetings = g.Key == now.Date ? g.Where(m => m.StartTime >= now).ToList() : g.ToList();
            if (dayMeetings.Count == 0) continue;
            var header = g.Key == now.Date ? "Сегодня" : g.Key.ToString("dddd, dd.MM.yyyy");
            var groupItem = new ListViewItem(new[] { header, "", "" }) { BackColor = System.Drawing.Color.FromArgb(0xEF, 0xF5, 0xFF) };
            _list.Items.Add(groupItem);
            foreach (var m in dayMeetings)
            {
                var li = new ListViewItem(new[] { m.StartTime.ToString("HH:mm"), m.Title, m.ProviderType.ToString() }) { Tag = m.JoinUrl };
                _list.Items.Add(li);
            }
        }
        _list.EndUpdate();

        // Update tray menu
        var menu = new ContextMenuStrip();
        // today meetings first
        var today = now.Date;
        var upcoming = items.Where(m => m.StartTime > now).OrderBy(m => m.StartTime).FirstOrDefault();
        foreach (var m in items.Where(m => m.StartTime.Date == today && m.StartTime > now))
        {
            var title = m.Title.Length > 30 ? m.Title.Substring(0, 30) + "…" : m.Title;
            var display = title;
            var isUpcoming = upcoming != null && object.ReferenceEquals(m, upcoming);
            if (isUpcoming)
            {
                var remaining = FormatRemaining(m.StartTime - now);
                display = $"{title} ({remaining})";
            }
            var mi = new ToolStripMenuItem(display) { ToolTipText = m.Title };
            var url = m.JoinUrl;
            mi.Click += (_, __) => { if (!string.IsNullOrWhiteSpace(url)) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { } } };
            if (isUpcoming)
            {
                var remaining = FormatRemaining(m.StartTime - now);
                mi.Tag = new BoldParts { Left = title + " ", Right = "(" + remaining + ")" };
            }
            menu.Items.Add(mi);
        }
        if (menu.Items.Count > 0) menu.Items.Add(new ToolStripSeparator());
        var about = new ToolStripMenuItem("О программе", null, (_, __) => { using var f = new AboutForm(); f.ShowDialog(this); });
        var settings = new ToolStripMenuItem("Настройки", null, (_, __) => { using var f = new SettingsForm(_settingsStore); if (f.ShowDialog(this) == DialogResult.OK) { _ = LoadMeetingsAsync(false); } });
        var exit = new ToolStripMenuItem("Выход", null, (_, __) => { try { _tray.Visible = false; } catch { } Application.Exit(); });
        menu.Items.Add(settings);
        menu.Items.Add(about);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);
        // Renderer, который рисует текст в скобках полужирным (для ближайшей встречи)
        menu.Renderer = new BoldParenRenderer();
        _tray.ContextMenuStrip = menu;

        // Update tray tooltip with nearest meeting time left
        if (upcoming != null)
        {
            var remaining = FormatRemaining(upcoming.StartTime - now);
            _tray.Text = $"Meetter — {remaining} до встречи";
        }
        else
        {
            _tray.Text = "Meetter";
        }
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
                if (delta <= notifyIn && delta > TimeSpan.Zero)
                {
                    // show toast
                    ShowToast($"Скоро встреча: {m.Title}", m.JoinUrl!);
                }
            }
        }
        catch { }
    }

    private void ShowToast(string title, string url)
    {
        // Fallback через balloon tip + быстрый переход по клику
        _tray.BalloonTipTitle = title.Length > 60 ? title.Substring(0, 60) + "…" : title;
        _tray.BalloonTipText = "Нажмите, чтобы подключиться";
        EventHandler? handler = null;
        handler = (_, __) =>
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            _tray.BalloonTipClicked -= handler;
        };
        _tray.BalloonTipClicked += handler;
        _tray.ShowBalloonTip(5000);
    }

    private static string FormatRemaining(TimeSpan delta)
    {
        if (delta.TotalSeconds < 1) return "0 мин";
        var days = (int)delta.TotalDays;
        var hours = delta.Hours;
        var minutes = delta.Minutes;
        if (days > 0) return hours > 0 ? $"{days} д {hours} ч" : $"{days} д";
        if (hours > 0) return minutes > 0 ? $"{hours} ч {minutes} мин" : $"{hours} ч";
        return minutes <= 0 ? "< 1 мин" : $"{minutes} мин";
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

            // базовый цвет учитывает выделение
            var color = e.TextColor;
            var x = e.TextRectangle.Left;
            var y = e.TextRectangle.Top + (e.TextRectangle.Height - e.TextFont.Height) / 2;
            var leftSize = TextRenderer.MeasureText(e.Graphics, left, e.TextFont, Size.Empty, flags);
            TextRenderer.DrawText(e.Graphics, left, e.TextFont, new Point(x, y), color, flags);
            x += leftSize.Width;
            using var bold = new Font(e.TextFont, FontStyle.Bold);
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

internal static class PathHelper
{
    public static string GetSettingsPath() => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "settings.json");
}

