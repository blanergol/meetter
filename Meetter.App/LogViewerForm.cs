using System.Text;

namespace Meetter.App;

public sealed class LogViewerForm : Form
{
    private readonly TextBox _text = new();
    private readonly System.Windows.Forms.Timer _timer;
    private long _lastLength;
    private readonly string _path;

    public LogViewerForm()
    {
        Text = "Logging";
        Icon = AppIconFactory.CreateIcon();
        Width = 900;
        Height = 600;
        StartPosition = FormStartPosition.CenterParent;
        _path = AppLogger.LogFilePath;

        var panelTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(8, 8, 8, 4), FlowDirection = FlowDirection.LeftToRight, WrapContents = false
        };
        var btnOpen = new Button { Text = "Open in Notepad", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        btnOpen.Click += (_, __) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_path)
                    { UseShellExecute = true });
            }
            catch
            {
            }
        };
        var btnClear = new Button { Text = "Clear", AutoSize = true, Margin = new Padding(0, 0, 8, 0) };
        btnClear.Click += (_, __) =>
        {
            try
            {
                File.WriteAllText(_path, string.Empty);
            }
            catch
            {
            }

            LoadContent();
        };
        var btnCopy = new Button { Text = "Copy", AutoSize = true };
        btnCopy.Click += (_, __) =>
        {
            try
            {
                Clipboard.SetText(_text.Text);
            }
            catch
            {
            }
        };
        panelTop.Controls.AddRange(new Control[] { btnOpen, btnClear, btnCopy });

        _text.Dock = DockStyle.Fill;
        _text.Multiline = true;
        _text.ReadOnly = true;
        _text.ScrollBars = ScrollBars.Both;
        _text.Font = new System.Drawing.Font("Consolas", 10);
        _text.WordWrap = false;

        Controls.Add(_text);
        Controls.Add(panelTop);

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, __) => LoadContent();
        Shown += (_, __) =>
        {
            LoadContent();
            _timer.Start();
        };
        FormClosed += (_, __) => _timer.Stop();
    }

    private void LoadContent()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            if (!File.Exists(_path))
            {
                File.WriteAllText(_path, string.Empty, Encoding.UTF8);
            }

            var fi = new FileInfo(_path);
            if (fi.Length == _lastLength) return;
            _lastLength = fi.Length;
            // limit to last 2 MB for large files
            const int maxRead = 2 * 1024 * 1024;
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length > maxRead) fs.Seek(-maxRead, SeekOrigin.End);
            using var sr = new StreamReader(fs, Encoding.UTF8);
            _text.Text = sr.ReadToEnd();
            _text.SelectionStart = _text.TextLength;
            _text.ScrollToCaret();
        }
        catch
        {
        }
    }
}