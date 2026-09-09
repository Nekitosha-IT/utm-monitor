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
        egais.DropDownItems.Add("Архив и загрузка за период…", null, (_, _) => { using var f = new EgaisArchiveAdvancedForm(); f.ShowDialog(form); });
        egais.DropDownItems.Add("Проверка марок выбранной ТТН через УТМ…", null, (_, _) => { using var f = new EgaisTtnMarkCheckForm(); f.ShowDialog(form); });
        egais.DropDownItems.Add("Массовая проверка акцизных марок…", null, (_, _) => { using var f = new MarkBatchForm(); f.ShowDialog(form); });
        menu.Items.Add(egais);
    }
}
