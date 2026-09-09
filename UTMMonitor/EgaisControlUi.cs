using System.Reflection;

namespace UTMMonitor;

public static class EgaisControlUi
{
    public static void Install(Form form)
    {
        var menu = form.MainMenuStrip;
        if (menu == null) return;
        var egais = new ToolStripMenuItem("ЕГАИС");
        egais.DropDownItems.Add("Центр контроля ЕГАИС…", null, (_, _) => { using var f = new EgaisControlForm(); f.ShowDialog(form); });
        egais.DropDownItems.Add("Контроль ТТН и марок…", null, (_, _) => { using var f = new EgaisDocumentControlForm(); f.ShowDialog(form); });
        egais.DropDownItems.Add("Архив и загрузка за период…", null, (_, _) =>
        {
            using var f = new EgaisArchiveForm();
            WireArchiveCard(f);
            f.ShowDialog(form);
        });
        egais.DropDownItems.Add("Проверка марок выбранной ТТН через УТМ…", null, (_, _) => { using var f = new EgaisTtnMarkCheckForm(); f.ShowDialog(form); });
        egais.DropDownItems.Add("Массовая проверка акцизных марок…", null, (_, _) => { using var f = new MarkBatchForm(); f.ShowDialog(form); });
        menu.Items.Add(egais);
    }

    static void WireArchiveCard(EgaisArchiveForm form)
    {
        var gridField = typeof(EgaisArchiveForm).GetField("grid", BindingFlags.Instance | BindingFlags.NonPublic);
        if (gridField?.GetValue(form) is not DataGridView grid) return;
        grid.CellDoubleClick += (_, e) =>
        {
            if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count) return;
            var r = grid.Rows[e.RowIndex];
            string V(string name) => grid.Columns.Contains(name) ? r.Cells[name].Value?.ToString() ?? "" : "";
            var mark = V("Марка"); if (string.IsNullOrWhiteSpace(mark)) return;
            using var card = new EgaisMarkArchiveDetailsForm(mark, V("Type"), V("Rank"), V("Number"), V("Товар"), V("Поставщик"), V("От_кого"), V("Кому"), V("ТТН"), V("Дата"), V("Статус"), $"Марка: {mark}\r\nТовар: {V("Товар")}\r\nПоставщик: {V("Поставщик")}\r\nОт кого: {V("От_кого")}\r\nКому: {V("Кому")}\r\nТТН: {V("ТТН")}\r\nДата: {V("Дата")}\r\nСтатус: {V("Статус")}");
            card.ShowDialog(form);
        };
    }
}
