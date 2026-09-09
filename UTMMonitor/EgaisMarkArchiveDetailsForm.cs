using System.Text;

namespace UTMMonitor;

public sealed class EgaisMarkArchiveDetailsForm : Form
{
    public EgaisMarkArchiveDetailsForm(EgaisArchiveForm.ArchiveDetail detail)
    {
        Text = "ЕГАИС — Карточка акцизной марки"; Width = 900; Height = 700; MinimumSize = new(760, 560); StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 11, Padding = new Padding(14), AutoScroll = true };
        root.ColumnStyles.Add(new(SizeType.Absolute, 180)); root.ColumnStyles.Add(new(SizeType.Percent, 100));
        Add(root, 0, "Акцизная марка", detail.Mark); Add(root, 1, "Type", detail.Type); Add(root, 2, "Rank", detail.Rank); Add(root, 3, "Number", detail.NumberPart);
        Add(root, 4, "Товар", detail.Product); Add(root, 5, "Поставщик", detail.Supplier); Add(root, 6, "От кого", detail.From); Add(root, 7, "Кому", detail.To);
        Add(root, 8, "Документ", detail.DocumentNumber); Add(root, 9, "Дата", detail.Date); Add(root, 10, "Статус проверки", detail.Status);
        var raw = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), Text = detail.Raw };
        var rawPage = new TabPage("Исходные данные"); rawPage.Controls.Add(raw);
        var tabs = new TabControl { Dock = DockStyle.Fill }; var info = new TabPage("Карточка"); info.Controls.Add(root); tabs.TabPages.Add(info); tabs.TabPages.Add(rawPage); Controls.Add(tabs);
    }
    static void Add(TableLayoutPanel p, int row, string name, string value)
    {
        p.RowStyles.Add(new(SizeType.AutoSize)); p.Controls.Add(new Label { Text = name, AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Padding = new Padding(0, 7, 0, 4) }, 0, row);
        p.Controls.Add(new TextBox { Text = value ?? "", ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Top, MinimumSize = new(0, 30) }, 1, row);
    }
}
