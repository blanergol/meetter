using Meetter.Persistence;
using Meetter.Providers.Google;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using MessageBox = System.Windows.MessageBox;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace Meetter.App;

public partial class SettingsWindow : Window
{
	private readonly ISettingsStore _store;
	private AppSettings _settings = new();
	public ObservableCollection<EmailAccount> Accounts { get; } = new();

	public SettingsWindow(ISettingsStore store)
	{
		InitializeComponent();
		Icon = IconHelper.CreateWindowIcon();
		_store = store;
		Loaded += async (_, __) =>
		{
			_settings = await _store.LoadAsync();
			Accounts.Clear();
			foreach (var a in _settings.Accounts) Accounts.Add(a);
			AccountsList.ItemsSource = Accounts;
			DaysCombo.SelectedIndex = Math.Clamp(_settings.DaysToShow, 1, 7) - 1;
		};
	}

	private async void OnAddGoogleAccount(object sender, RoutedEventArgs e)
	{
		try
		{
			// Берём credentials.json из папки exe. Если нет — просим выбрать (без копирования/кеширования)
			string credentialsPath;
			var exeCreds = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.json");
			if (File.Exists(exeCreds))
			{
				credentialsPath = exeCreds;
			}
			else
			{
				var dlg = new OpenFileDialog
				{
					Title = "Выберите credentials.json",
					Filter = "JSON (*.json)|*.json|Все файлы (*.*)|*.*",
					FileName = "credentials.json"
				};
				if (dlg.ShowDialog(this) != true) return;
				credentialsPath = dlg.FileName;
			}
			var appName = "Meetter";

			var tempTokenDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "google-temp-" + Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(tempTokenDir);

			UserCredential credential;
			await using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
			{
				var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
				credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
					secrets,
					new[] { CalendarService.Scope.CalendarReadonly },
					"user",
					CancellationToken.None,
					new FileDataStore(tempTokenDir, true));
			}

			using var service = new CalendarService(new BaseClientService.Initializer
			{
				HttpClientInitializer = credential,
				ApplicationName = appName,
			});

			var list = await service.CalendarList.List().ExecuteAsync();
			var primary = list.Items?.FirstOrDefault(i => i.Primary == true) ?? list.Items?.FirstOrDefault();
			if (primary == null || string.IsNullOrWhiteSpace(primary.Id))
			{
				MessageBox.Show(this, "Не удалось определить email аккаунта", "Добавление аккаунта", MessageBoxButton.OK, MessageBoxImage.Warning);
				return;
			}

			var email = primary.Id;
			var finalTokenDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "google", email);
			Directory.CreateDirectory(System.IO.Path.GetDirectoryName(finalTokenDir)!);
			if (Directory.Exists(finalTokenDir)) Directory.Delete(finalTokenDir, true);
			Directory.Move(tempTokenDir, finalTokenDir);

			if (Accounts.Any(a => a.ProviderId == GoogleCalendarProvider.ProviderKey && a.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
			{
				MessageBox.Show(this, "Этот аккаунт уже добавлен", "Добавление аккаунта", MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			Accounts.Add(new EmailAccount
			{
				ProviderId = GoogleCalendarProvider.ProviderKey,
				Email = email,
				DisplayName = email,
				Enabled = true,
				Properties = { ["credentialsPath"] = credentialsPath, ["tokenPath"] = finalTokenDir }
			});
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, "Ошибка добавления аккаунта", MessageBoxButton.OK, MessageBoxImage.Error);
		}
	}

	private void OnRemoveAccount(object sender, RoutedEventArgs e)
	{
		if (AccountsList.SelectedItem is EmailAccount acc)
		{
			Accounts.Remove(acc);
		}
	}

	private async void OnSave(object sender, RoutedEventArgs e)
	{
		_settings.Accounts = Accounts.ToList();
		_settings.DaysToShow = DaysCombo.SelectedIndex + 1;
		await _store.SaveAsync(_settings);
		DialogResult = true;
		Close();
	}

	private void OnCancel(object sender, RoutedEventArgs e)
	{
		DialogResult = false;
		Close();
	}
}

