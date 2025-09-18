namespace Meetter.App;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About";
        Icon = AppIconFactory.CreateIcon();
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(450, 350);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        // Top row stretches, bottom row for buttons docked to bottom
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        // Контент: верхняя область + нижняя панель с версией и разработчиком
        var content = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink
        };
        content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        content.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lblDescription = new Label
        {
            Text =
                "Meetter - windows application that aggregates meetings from calendars, extracts Google Meet/Zoom links, and displays them in a convenient list.",
            AutoSize = true,
            TextAlign = ContentAlignment.MiddleCenter,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0, 0, 0, 8)
        };

        var lblVersion = new Label
        {
            Text = "Version 1.0.2",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 2)
        };

        var lblDeveloper = new Label
        {
            Text = "Developer: blanergol",
            AutoSize = true
        };

        var topPanel = new Panel { Dock = DockStyle.Fill, AutoSize = false };
        topPanel.Controls.Add(lblDescription);
        lblDescription.Location = new Point(0, 0);
        lblDescription.Dock = DockStyle.Top;

        var bottomInfo = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Padding = new Padding(0, 8, 0, 0)
        };
        bottomInfo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomInfo.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        bottomInfo.Controls.Add(lblVersion, 0, 0);
        bottomInfo.Controls.Add(lblDeveloper, 0, 1);

        content.Controls.Add(topPanel, 0, 0);
        content.Controls.Add(bottomInfo, 0, 1);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true,
            Padding = new Padding(0, 12, 0, 0)
        };
        var ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(ok);

        root.Controls.Add(content, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);

        void UpdateWrapWidth()
        {
            var availableWidth = root.ClientSize.Width - root.Padding.Horizontal;
            if (availableWidth < 100) availableWidth = 100;
            lblDescription.MaximumSize = new Size(availableWidth, 0);
        }

        root.SizeChanged += (_, __) => UpdateWrapWidth();
        UpdateWrapWidth();
    }
}