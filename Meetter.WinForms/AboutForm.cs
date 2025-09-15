using System.Windows.Forms;
using System.Drawing;

namespace Meetter.WinForms;

public sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "О программе";
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F);
        MinimumSize = new Size(420, 200);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(12),
            AutoSize = true
        };
        // Верхняя строка тянется, нижняя под кнопки прижата к низу
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lbl = new Label { Text = "Meetter\nВерсия 0.2 (WinForms)", AutoSize = true };
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, Padding = new Padding(0, 12, 0, 0) };
        var ok = new Button { Text = "ОК", DialogResult = DialogResult.OK, AutoSize = true };
        buttons.Controls.Add(ok);
        root.Controls.Add(lbl, 0, 0);
        root.Controls.Add(buttons, 0, 1);
        Controls.Add(root);
    }
}

