using System.Drawing;
using System.Windows.Forms;

namespace UTMMonitor;

public static class LegacyTheme
{
    static readonly Color Bg = Color.FromArgb(7, 11, 20);
    static readonly Color Panel = Color.FromArgb(15, 23, 42);
    static readonly Color Panel2 = Color.FromArgb(10, 16, 28);
    static readonly Color Border = Color.FromArgb(38, 51, 72);
    static readonly Color Text = Color.FromArgb(241, 245, 249);
    static readonly Color Muted = Color.FromArgb(132, 146, 168);

    public static void Apply(Form form)
    {
        form.BackColor = Bg;
        form.ForeColor = Text;
        form.Font = new Font("Segoe UI", 9.5F);
        ApplyControl(form);
    }

    static void ApplyControl(Control c)
    {
        switch (c)
        {
            case TabControl tabs:
                tabs.BackColor = Bg; tabs.ForeColor = Text; break;
            case TabPage page:
                page.BackColor = Bg; page.ForeColor = Text; break;
            case Panel panel:
                panel.BackColor = Panel; break;
            case GroupBox group:
                group.BackColor = Panel; group.ForeColor = Text; break;
            case Label label:
                label.ForeColor = label.Font.Size <= 11 ? Muted : Text; break;
            case DataGridView grid:
                StyleGrid(grid); break;
            case TextBox box:
                box.BackColor = Color.FromArgb(9, 15, 27); box.ForeColor = Text; box.BorderStyle = BorderStyle.FixedSingle; break;
            case ComboBox combo:
                combo.BackColor = Color.FromArgb(17, 25, 40); combo.ForeColor = Text; break;
            case DateTimePicker date:
                date.BackColor = Panel; date.ForeColor = Text; date.CalendarMonthBackground = Panel; date.CalendarForeColor = Text; break;
            case Button button:
                StyleButton(button); break;
        }
        foreach (Control child in c.Controls) ApplyControl(child);
    }

    static void StyleGrid(DataGridView grid)
    {
        grid.BackgroundColor = Color.FromArgb(8, 13, 23);
        grid.GridColor = Border;
        grid.BorderStyle = BorderStyle.None;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(8, 13, 23);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Muted;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(8, 13, 23);
        grid.DefaultCellStyle.BackColor = Panel2;
        grid.DefaultCellStyle.ForeColor = Text;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(24, 43, 70);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.RowTemplate.Height = 34;
    }

    static void StyleButton(Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = Color.FromArgb(31, 49, 78);
        button.FlatAppearance.MouseDownBackColor = Color.FromArgb(24, 39, 62);
        button.BackColor = Color.FromArgb(18, 29, 47);
        button.ForeColor = Text;
        button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }
}
