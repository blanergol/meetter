using Meetter.Persistence;
using Meetter.Providers.Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System;
using System.Linq;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Meetter.WinForms;

public sealed class SettingsForm : Form
{
    private readonly ISettingsStore _store;
    private NumericUpDown _days;
    private Button _ok;
    private Button _cancel;
    private BindingSource _accountsSource = new();
    private System.Collections.Generic.List<EmailAccount> _accounts = new();
    private ListView _accountsList;
    private Button _addGoogleBtn;
    private Button _removeBtn;
    private CheckBox _autoStart;

    public SettingsForm(ISettingsStore store)
    {
        _store = store;
        Text = "Настройки";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        AutoSize = false;
        AutoSizeMode = AutoSizeMode.GrowOnly;
        MinimumSize = new Size(640, 420);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(12),
            AutoSize = false,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        // Контентная строка занимает всё пространство, нижняя — автосайз для кнопок
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Tabs
        var tabs = new TabControl { Dock = DockStyle.Fill };

        // Accounts tab
        var tabAccounts = new TabPage("Аккаунты") { Padding = new Padding(6) };
        var accLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, AutoSize = false };
        accLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        accLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Список аккаунтов через ListView (легче адаптируется по DPI)
        _accountsList = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            CheckBoxes = true
        };
        _accountsList.Columns.Add("Вкл.", 60);
        _accountsList.Columns.Add("Провайдер", 120);
        _accountsList.Columns.Add("Email", 260);
        _accountsList.Columns.Add("Имя", 220);
        _accountsList.ItemChecked += (_, e) =>
        {
            var idx = e.Item.Index;
            if (idx >= 0 && idx < _accounts.Count)
            {
                _accounts[idx].Enabled = e.Item.Checked;
            }
        };
        // no inline edit panel; selection change not used

        var accButtons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 8, 0, 0) };
        _addGoogleBtn = new Button { Text = "Добавить Google аккаунт", AutoSize = true };
        _removeBtn = new Button { Text = "Удалить", AutoSize = true };
        _addGoogleBtn.Click += async (_, __) => await OnAddGoogleAccountAsync();
        _removeBtn.Click += (_, __) => OnRemoveAccount();
        accButtons.Controls.Add(_removeBtn);
        accButtons.Controls.Add(_addGoogleBtn);
        accLayout.Controls.Add(_accountsList, 0, 0);
        accLayout.Controls.Add(accButtons, 0, 1);

        // панель редактирования имени убрана по требованию
        tabAccounts.Controls.Add(accLayout);

        // General tab
        var tabGeneral = new TabPage("Общие") { Padding = new Padding(12) };
        var generalLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, AutoSize = true };
        generalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        generalLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        generalLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        generalLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        generalLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        generalLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        var lbl = new Label { Text = "Дней показывать:", AutoSize = true, Anchor = AnchorStyles.Left };
        _days = new NumericUpDown { Width = 100, Minimum = 1, Maximum = 7, Value = 3, Anchor = AnchorStyles.Left };
        generalLayout.Controls.Add(lbl, 0, 0);
        generalLayout.Controls.Add(_days, 1, 0);
        var notifLbl = new Label { Text = "Уведомлять за (мин):", AutoSize = true, Anchor = AnchorStyles.Left };
        var notifNum = new NumericUpDown { Width = 100, Minimum = 0, Maximum = 120, Value = 5, Anchor = AnchorStyles.Left };
        generalLayout.Controls.Add(notifLbl, 0, 1);
        generalLayout.Controls.Add(notifNum, 1, 1);
        var autoLbl = new Label { Text = "Запускать при старте Windows:", AutoSize = true, Anchor = AnchorStyles.Left };
        _autoStart = new CheckBox { AutoSize = true, Anchor = AnchorStyles.Left };
        generalLayout.Controls.Add(autoLbl, 0, 2);
        generalLayout.Controls.Add(_autoStart, 1, 2);
        tabGeneral.Controls.Add(generalLayout);

        tabs.TabPages.Add(tabAccounts);
        tabs.TabPages.Add(tabGeneral);

        // Buttons strip
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Padding = new Padding(0, 12, 0, 0)
        };
        _ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, AutoSize = true };
        _cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, AutoSize = true };
        buttons.Controls.Add(_cancel);
        buttons.Controls.Add(_ok);
        root.SetColumnSpan(buttons, 2);
        root.Controls.Add(tabs, 0, 0);
        root.SetColumnSpan(tabs, 2);
        root.Controls.Add(buttons, 0, 1);

        _ok.Click += async (_, __) =>
        {
            var s = await _store.LoadAsync();
            s.DaysToShow = (int)_days.Value;
            s.NotifyMinutes = (int)notifNum.Value;
            s.Accounts = _accounts.ToList();
            s.AutoStart = _autoStart.Checked;
            await _store.SaveAsync(s);
            try { AutoStartManager.SetAutoStart(s.AutoStart); } catch { }
            Close();
        };

        Controls.Add(root);
        AcceptButton = _ok;
        CancelButton = _cancel;

        Load += async (_, __) =>
        {
            var s = await _store.LoadAsync();
            _days.Value = Math.Clamp(s.DaysToShow, 1, 7);
            notifNum.Value = Math.Clamp(s.NotifyMinutes, 0, 120);
            _autoStart.Checked = s.AutoStart || AutoStartManager.IsEnabled();
            _accounts = s.Accounts.ToList();
            _accountsSource.DataSource = _accounts;
            RefreshAccountsList();
        };
    }

    private async Task OnAddGoogleAccountAsync()
    {
        try
        {
            // credentials.json рядом с exe, иначе спросим
            string credentialsPath;
            var exeCreds = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.json");
            if (File.Exists(exeCreds)) credentialsPath = exeCreds;
            else
            {
                using var dlg = new OpenFileDialog { Title = "Выберите credentials.json", Filter = "JSON (*.json)|*.json|Все файлы (*.*)|*.*", FileName = "credentials.json" };
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                credentialsPath = dlg.FileName;
            }

            var tempTokenDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "google-temp-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempTokenDir);

            UserCredential credential;
            await using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
            {
                var secrets = GoogleClientSecrets.FromStream(stream).Secrets;
                credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                    secrets, new[] { CalendarService.Scope.CalendarReadonly }, "user", CancellationToken.None,
                    new FileDataStore(tempTokenDir, true));
            }

            using var service = new CalendarService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Meetter",
            });
            var list = await service.CalendarList.List().ExecuteAsync();
            var primary = list.Items?.FirstOrDefault(i => i.Primary == true) ?? list.Items?.FirstOrDefault();
            if (primary == null || string.IsNullOrWhiteSpace(primary.Id))
            {
                MessageBox.Show(this, "Не удалось определить email аккаунта", "Добавление аккаунта", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var email = primary.Id;
            var finalTokenDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Meetter", "google", email);
            Directory.CreateDirectory(Path.GetDirectoryName(finalTokenDir)!);
            if (Directory.Exists(finalTokenDir)) Directory.Delete(finalTokenDir, true);
            Directory.Move(tempTokenDir, finalTokenDir);

            if (_accounts.Any(a => a.ProviderId == GoogleCalendarProvider.ProviderKey && a.Email.Equals(email, StringComparison.OrdinalIgnoreCase)))
            {
                MessageBox.Show(this, "Этот аккаунт уже добавлен", "Добавление аккаунта", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            _accounts.Add(new EmailAccount
            {
                ProviderId = GoogleCalendarProvider.ProviderKey,
                Email = email,
                DisplayName = email,
                Enabled = true,
                Properties = { ["credentialsPath"] = credentialsPath, ["tokenPath"] = finalTokenDir }
            });
            _accountsSource.ResetBindings(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Ошибка добавления аккаунта", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnRemoveAccount()
    {
        if (_accountsList.SelectedIndices.Count == 1)
        {
            var idx = _accountsList.SelectedIndices[0];
            _accounts.RemoveAt(idx);
            _accountsSource.ResetBindings(false);
            RefreshAccountsList();
        }
    }

    private void RefreshAccountsList()
    {
        _accountsList.BeginUpdate();
        _accountsList.Items.Clear();
        foreach (var a in _accounts)
        {
            var item = new ListViewItem { Checked = a.Enabled };
            item.SubItems.Add(a.ProviderId);
            item.SubItems.Add(a.Email);
            item.SubItems.Add(a.DisplayName);
            _accountsList.Items.Add(item);
        }
        for (int i = 0; i < _accountsList.Columns.Count; i++)
        {
            _accountsList.AutoResizeColumn(i, ColumnHeaderAutoResizeStyle.ColumnContent);
        }
        _accountsList.EndUpdate();
    }

}

