namespace UTMMonitor;

public static class MarkBatchUi
{
    public static void Install(Form form)
    {
        var menu = new MenuStrip();
        var marks = new ToolStripMenuItem("Марки");
        marks.DropDownItems.Add("Массовая проверка акцизных марок…", null, (_, _) =>
        {
            using var f = new MarkBatchForm();
            f.ShowDialog(form);
        });
        marks.DropDownItems.Add(new ToolStripSeparator());
        marks.DropDownItems.Add("Проверка марки — вкладка «Проверка марки»", null, (_, _) => SelectMarksTab(form));
        menu.Items.Add(marks);
        form.MainMenuStrip = menu;
        form.Controls.Add(menu);
        menu.BringToFront();
    }

    static void SelectMarksTab(Form form)
    {
        foreach (var c in form.Controls)
            if (c is TableLayoutPanel root)
                foreach (var child in root.Controls)
                    if (child is TabControl tabs && tabs.TabPages.Count > 2) { tabs.SelectedIndex = 2; return; }
    }
}
