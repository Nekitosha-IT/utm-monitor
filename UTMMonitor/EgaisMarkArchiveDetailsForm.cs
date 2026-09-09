namespace UTMMonitor;

public sealed class EgaisMarkArchiveDetailsForm : Form
{
    public EgaisMarkArchiveDetailsForm(string mark, string type, string rank, string numberPart, string product, string supplier, string from, string to, string documentNumber, string date, string status, string raw)
    {
        Text = "ЕГАИС — Карточка акцизной марки"; Width = 900; Height = 700; MinimumSize = new(760, 560); StartPosition = FormStartPosition.CenterParent;
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 11, Padding = new Padding(14), AutoScroll = true };
        root.ColumnStyles.Add(new(SizeType.Absolute, 180)); root.ColumnStyles.Add(new(SizeType.Percent, 100));
        Add(root, 0, "Акцизная марка", mark); Add(root, 1, "Type", type); Add(root, 2, "Rank", rank); Add(root, 3, "Number", numberPart);
        Add(root, 4, "Товар", product); Add(root, 5, "Поставщик", supplier); Add(root, 6, "От кого", from); Add(root, 7, "Кому", to);
        Add(root, 8, "Документ / ТТН", documentNumber); Add(root, 9, "Дата", date); Add(root, 10, "Статус проверки", status);
        var rawBox = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, Dock = DockStyle.Fill, Font = new Font("Consolas", 9), Text = raw ?? "" };
        var tabs = new TabControl { Dock = DockStyle.Fill }; var info = new TabPage("Карточка"); info.Controls.Add(root); var rawPage = new TabPage("Исходные данные"); rawPage.Controls.Add(rawBox); tabs.TabPages.Add(info); tabs.TabPages.Add(rawPage); Controls.Add(tabs);
    }
    static void Add(TableLayoutPanel p, int row, string name, string value)
    {
        p.RowStyles.Add(new(SizeType.AutoSize)); p.Controls.Add(new Label { Text = name, AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Padding = new Padding(0, 7, 0, 4) }, 0, row);
        p.Controls.Add(new TextBox { Text = value ?? "", ReadOnly = true, BorderStyle = BorderStyle.FixedSingle, Dock = DockStyle.Top, MinimumSize = new(0, 30) }, 1, row);
    }
}
