using Meetter.Core;
using Meetter.Persistence;
using Meetter.Providers.Google;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Data;

namespace Meetter.App;

public partial class MainWindow : Window
{
	private readonly ISettingsStore _settingsStore;

	public MainWindow()
	{
		InitializeComponent();
		_settingsStore = new JsonSettingsStore(PathHelper.GetSettingsPath());
		Loaded += async (_, __) => { await LoadMeetingsAsync(useCache: true); };
	}

	private async Task LoadMeetingsAsync(bool useCache)
	{
		try
		{
			LoaderOverlay.Visibility = Visibility.Visible;
			RefreshButton.IsEnabled = false;
			Logger.Info("LoadMeetingsAsync: start");
			var settings = await _settingsStore.LoadAsync();
			Logger.Info($"Settings: DaysToShow={settings.DaysToShow}, Accounts={settings.Accounts.Count}");
			var detectors = new IMeetingLinkDetector[] { new GoogleMeetLinkDetector(), new ZoomLinkDetector() };
			var providers = new List<ICalendarProvider>();
			foreach (var acc in settings.Accounts.Where(a => a.Enabled))
			{
				if (acc.ProviderId == GoogleCalendarProvider.ProviderKey)
				{
					var creds = acc.Properties.TryGetValue("credentialsPath", out var cp) ? cp : "credentials.json";
					var token = acc.Properties.TryGetValue("tokenPath", out var tp) ? tp : System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "google", acc.Email);
					Logger.Info($"Provider: Google email={acc.Email} creds={creds} token={token}");
					providers.Add(new GoogleCalendarProvider(detectors, "Meetter", creds, token));
				}
			}
			var aggregator = new MeetingsAggregator(providers);

			var from = DateTimeOffset.Now.Date;
			var to = from.AddDays(Math.Clamp(settings.DaysToShow, 1, 7));
			Logger.Info($"Fetching range: {from:u}..{to:u}");
			var cache = new FileMeetingsCache();
			if (useCache)
			{
				var (hit, cached) = await cache.TryReadAsync(from, to);
				if (hit)
				{
					Logger.Info($"Cache hit: {cached.Count} meetings");
					BindMeetings(cached);
					return;
				}
			}
			var items = await aggregator.GetMeetingsAsync(from, to, CancellationToken.None);
			Logger.Info($"Fetched: {items.Count} meetings");
			await cache.WriteAsync(from, to, items);
			BindMeetings(items);
		}
		catch (Exception ex)
		{
			Logger.Error("LoadMeetingsAsync error", ex);
			MessageBox.Show(this, ex.Message, "Ошибка загрузки", MessageBoxButton.OK, MessageBoxImage.Error);
		}
		finally
		{
			LoaderOverlay.Visibility = Visibility.Collapsed;
			RefreshButton.IsEnabled = true;
		}
	}

	private void BindMeetings(IReadOnlyList<Meeting> items)
	{
		var cvs = new ListCollectionView(items.ToList());
		cvs.GroupDescriptions.Clear();
		cvs.GroupDescriptions.Add(new PropertyGroupDescription("StartTime", new DateOnlyGroupConverter()));
		MeetingsList.ItemsSource = cvs;
	}

	private async void OnRefresh(object sender, RoutedEventArgs e)
	{
		await LoadMeetingsAsync(useCache: false);
	}

	private void OnJoinMeeting(object sender, RoutedEventArgs e)
	{
		if (sender is System.Windows.Controls.Button btn && btn.Tag is string url && !string.IsNullOrWhiteSpace(url))
		{
			try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
			catch (Exception ex) { MessageBox.Show(this, ex.Message, "Не удалось открыть ссылку"); }
		}
	}

	private void OnOpenSettings(object sender, RoutedEventArgs e)
	{
		var w = new SettingsWindow(_settingsStore);
		w.Owner = this;
		if (w.ShowDialog() == true)
		{
			_ = LoadMeetingsAsync(useCache: false);
		}
	}

	private void OnOpenAbout(object sender, RoutedEventArgs e)
	{
		new AboutWindow { Owner = this }.ShowDialog();
	}

	private void OnExit(object sender, RoutedEventArgs e) => Close();
}

internal static class PathHelper
{
	public static string GetSettingsPath() => System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "settings.json");
}

