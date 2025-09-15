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

    public MainForm()
    {
        Text = "Meetter";
        Width = 900;
        Height = 600;

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

        _settingsStore = new JsonSettingsStore(PathHelper.GetSettingsPath());
        Shown += async (_, __) => await LoadMeetingsAsync(true);
        _list.DoubleClick += (_, __) =>
        {
            if (_list.SelectedItems.Count == 1 && _list.SelectedItems[0].Tag is string url && !string.IsNullOrWhiteSpace(url))
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            }
        };
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
        _list.BeginUpdate();
        _list.Items.Clear();
        // группировка по дням — визуально вставим заголовки-строки
        var groups = items.GroupBy(m => m.StartTime.Date).OrderBy(g => g.Key);
        foreach (var g in groups)
        {
            var header = g.Key == DateTimeOffset.Now.Date ? "Сегодня" : g.Key.ToString("dddd, dd.MM.yyyy");
            var groupItem = new ListViewItem(new[] { header, "", "" }) { BackColor = System.Drawing.Color.FromArgb(0xEF, 0xF5, 0xFF) };
            _list.Items.Add(groupItem);
            foreach (var m in g)
            {
                var li = new ListViewItem(new[] { m.StartTime.ToString("HH:mm"), m.Title, m.ProviderType.ToString() }) { Tag = m.JoinUrl };
                _list.Items.Add(li);
            }
        }
        _list.EndUpdate();
    }
}

internal static class PathHelper
{
    public static string GetSettingsPath() => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "settings.json");
}

