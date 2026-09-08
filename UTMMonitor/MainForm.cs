using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace UTMMonitor;

public sealed class MainForm : Form
{
    readonly UtmDatabase db = new();
    readonly UtmClient api = new();
    readonly Dictionary<int, UtmStatus> statuses = new();
    readonly DataGridView utmGrid = Grid();
    readonly DataGridView docGrid = Grid();
    readonly DataGridView markGrid = Grid();
    readonly DataGridView certGrid = Grid();
    readonly DataGridView historyGrid = Grid();
    readonly DataGridView eventGrid = Grid();
    readonly ComboBox utmPick = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 300 };
    readonly TextBox docSearch = new() { Width = 260 };
    readonly TextBox markSearch = new() { Width = 260 };
    readonly TextBox log = new() { Multiline = true, Dock = DockStyle.Fill, ReadOnly = true, ScrollBars = ScrollBars.Both };
    readonly Label state = new() { AutoSize = true, Text = "Готово" };
    readonly Label lastSync = new() { AutoSize = true, Text = "Последняя синхронизация: ещё не выполнялась" };
    readonly Timer timer = new() { Interval = 600000 };
    readonly Label cardUtms = CardValue("0");
    readonly Label cardOnline = CardValue("0");
    readonly Label cardDocs = CardValue("0");
    readonly Label cardMarks = CardValue("0");
    readonly Label cardAttention = CardValue("0");
    readonly Label cardCertificates = CardValue("0");
    readonly Label hint = new() { AutoSize = true, ForeColor = Color.DimGray };
    bool busy;

    public MainForm()
    {
        Text = "UTM Monitor — ЕГАИС";
        Width = 1500;
        Height = 900;
        MinimumSize = new(1180, 720);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new("Segoe UI", 9.5F);
        BackColor = Color.FromArgb(245, 247, 250);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(0) };
        root.RowStyles.Add(new(SizeType.Absolute, 64));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildTabs(), 0, 1);
        Controls.Add(root);

        utmGrid.SelectionChanged += (_, _) => OnUtmChanged();
        utmGrid.CellDoubleClick += (_, _) => Edit();
        docGrid.CellDoubleClick += (_, _) => OpenDocument();
        docSearch.TextChanged += (_, _) => ApplyDocumentFilter();
        markSearch.TextChanged += (_, _) => ApplyMarkFilter();
        utmPick.SelectedIndexChanged += (_, _) => OnUtmChanged();

        Load += async (_, _) =>
        {
            RefreshUtms();
            await CheckAll();
            var u = SelectedUtm();
            if (u != null)
            {
                await Sync(u, false);
                await LoadCertificates(false);
                LoadAllForSelected();
            }
        };
        timer.Tick += async (_, _) =>
        {
            if (busy) return;
            await CheckAll();
            foreach (var u in db.All().Where(x => x.Enabled)) await Sync(u, false);
            LoadAllForSelected();
        };
        timer.Start();
        FormClosed += (_, _) => { timer.Stop(); api.Dispose(); };
    }

    Control BuildHeader()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(31, 41, 55), Padding = new Padding(18, 10, 18, 10) };
        var title = new Label { Text = "UTM Monitor", ForeColor = Color.White, Font = new("Segoe UI Semibold", 18F), AutoSize = true, Location = new(18, 7) };
        var sub = new Label { Text = "Мониторинг УТМ и документов ЕГАИС", ForeColor = Color.Gainsboro, AutoSize = true, Location = new(170, 13) };
        state.ForeColor = Color.White;
        state.Location = new Point(0, 40);
        state.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        panel.Controls.Add(title); panel.Controls.Add(sub);
        panel.Controls.Add(state);
        panel.Resize += (_, _) => state.Location = new Point(panel.Width - state.PreferredWidth - 18, 22);
        return panel;
    }

    TabControl BuildTabs()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 8) };
        tabs.TabPages.Add(BuildDashboard());
        tabs.TabPages.Add(BuildDocuments());
        tabs.TabPages.Add(BuildMarks());
        tabs.TabPages.Add(BuildCertificates());
        tabs.TabPages.Add(BuildHistory());
        tabs.TabPages.Add(BuildLog());
        return tabs;
    }

    TabPage BuildDashboard()
    {
        var p = new TabPage("  Главная  ");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, Padding = new Padding(14), BackColor = Color.FromArgb(245, 247, 250) };
        root.RowStyles.Add(new(SizeType.Absolute, 108));
        root.RowStyles.Add(new(SizeType.Absolute, 58));
        root.RowStyles.Add(new(SizeType.Percent, 100));
        var cards = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, AutoScroll = true };
        cards.Controls.Add(Card("УТМ всего", cardUtms));
        cards.Controls.Add(Card("В сети", cardOnline));
        cards.Controls.Add(Card("Документы", cardDocs));
        cards.Controls.Add(Card("Марки", cardMarks));
        cards.Controls.Add(Card("Требуют внимания", cardAttention));
        cards.Controls.Add(Card("Сертификаты", cardCertificates));
        root.Controls.Add(cards, 0, 0);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
        actions.Controls.Add(Primary("Проверить УТМ", async () => await CheckAll()));
        actions.Controls.Add(Primary("Синхронизировать", async () => { var u = SelectedUtm(); if (u != null) await Sync(u, true); }));
        actions.Controls.Add(Button("Добавить УТМ", Add));
        actions.Controls.Add(Button("Изменить", Edit));
        actions.Controls.Add(Button("Удалить", Delete));
        actions.Controls.Add(Button("Открыть данные", OpenDataFolder));
        actions.Controls.Add(lastSync);
        root.Controls.Add(actions, 0, 1);

        var split = new SplitContainer { Dock = DockStyle.Fill, Orientation = Orientation.Horizontal, SplitterDistance = 180 };
        split.Panel1.Padding = new Padding(0, 5, 0, 5);
        split.Panel1.Controls.Add(utmGrid);
        var info = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Padding = new Padding(14) };
        hint.Text = "Выберите УТМ. Здесь видно главное: связь, скорость ответа, версия и идентификатор ФСРАР. Двойной щелчок — изменить настройки.";
        info.Controls.Add(hint);
        split.Panel2.Controls.Add(info);
        root.Controls.Add(split, 0, 2);
        p.Controls.Add(root);
        return p;
    }

    TabPage BuildDocuments()
    {
        var p = new TabPage("  Документы  ");
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, WrapContents = false };
        top.Controls.Add(LabelText("УТМ:")); top.Controls.Add(utmPick);
        top.Controls.Add(Button("Обновить", async () => { var u = SelectedUtm(); if (u != null) await Sync(u, true); }));
        top.Controls.Add(LabelText("Поиск:")); top.Controls.Add(docSearch);
        top.Controls.Add(Button("Открыть документ", OpenDocument));
        top.Controls.Add(Button("Экспорт", ExportDocuments));
        root.Controls.Add(docGrid); root.Controls.Add(top); p.Controls.Add(root);
        return p;
    }

    TabPage BuildMarks()
    {
        var p = new TabPage("  Проверка марки  ");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, Padding = new Padding(14) };
        root.RowStyles.Add(new(SizeType.Absolute, 310)); root.RowStyles.Add(new(SizeType.Percent, 100));
        var box = new GroupBox { Text = "Проверить акцизную марку через УТМ", Dock = DockStyle.Fill, Padding = new Padding(12) };
        var form = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6 };
        form.ColumnStyles.Add(new(SizeType.Absolute, 170)); form.ColumnStyles.Add(new(SizeType.Percent, 100));
        var raw = new TextBox { Multiline = true, Height = 55, Dock = DockStyle.Fill };
        var type = new TextBox(); var rank = new TextBox(); var number = new TextBox();
        var result = new TextBox { Multiline = true, ReadOnly = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical };
        form.Controls.Add(LabelText("DataMatrix / код:"), 0, 0); form.Controls.Add(raw, 1, 0);
        form.Controls.Add(LabelText("Type:"), 0, 1); form.Controls.Add(type, 1, 1);
        form.Controls.Add(LabelText("Rank:"), 0, 2); form.Controls.Add(rank, 1, 2);
        form.Controls.Add(LabelText("Number:"), 0, 3); form.Controls.Add(number, 1, 3);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        buttons.Controls.Add(Primary("Разобрать код", () => { var m = ParseMark(raw.Text); type.Text = m?.Type ?? ""; rank.Text = m?.Rank ?? ""; number.Text = m?.Number ?? ""; result.Text = m == null ? "Не удалось распознать код." : $"Код распознан. Type={m.Type}, Rank={m.Rank}, Number={m.Number}. Теперь нажмите «Проверить в УТМ»."; }));
        buttons.Controls.Add(Primary("Проверить в УТМ", async () =>
        {
            var u = SelectedUtm(); if (u == null) { result.Text = "Сначала выберите УТМ на вкладке «Главная»."; return; }
            var m = new MarkParts(type.Text.Trim(), rank.Text.Trim(), number.Text.Trim(), raw.Text);
            if (m.Type == "" || m.Rank == "" || m.Number == "") { result.Text = "Нужно указать Type, Rank и Number. Можно вставить полный DataMatrix и нажать «Разобрать код»."; return; }
            state.Text = "Проверяю марку…";
            var q = await api.QueryBarcodeAsync(u, m);
            var text = new StringBuilder(); text.AppendLine(q.Message); if (q.Ticket != null) text.AppendLine($"Идентификатор запроса: {q.Ticket}"); text.AppendLine(); text.AppendLine("Ответ отправки:"); text.AppendLine(q.Raw);
            if (q.Success && q.Ticket != null) { var t = await api.PollTicketAsync(u, q.Ticket); text.AppendLine(); text.AppendLine("Ответ УТМ:"); text.AppendLine(t.Raw); db.SaveMarkResult(u.Id, null, raw.Text, m, t.Success ? "OK" : "TIMEOUT", t.Raw); db.Event(u.Id, t.Success ? "INFO" : "ERROR", $"Проверка марки {q.Ticket}", t.Raw); }
            result.Text = text.ToString(); state.Text = "Готово"; await LoadMarks();
        }));
        buttons.Controls.Add(Button("Очистить", () => { raw.Clear(); type.Clear(); rank.Clear(); number.Clear(); result.Clear(); }));
        form.Controls.Add(buttons, 1, 4); form.Controls.Add(result, 1, 5); box.Controls.Add(form);
        root.Controls.Add(box, 0, 0);
        var history = new Panel { Dock = DockStyle.Fill };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        top.Controls.Add(LabelText("Поиск марки:")); top.Controls.Add(markSearch); top.Controls.Add(Button("Обновить", async () => await LoadMarks())); top.Controls.Add(Button("Экспорт", ExportMarks));
        history.Controls.Add(markGrid); history.Controls.Add(top); root.Controls.Add(history, 0, 1); p.Controls.Add(root);
        return p;
    }

    TabPage BuildCertificates()
    {
        var p = new TabPage("  Сертификаты  ");
        var root = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14) };
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 50, WrapContents = false };
        top.Controls.Add(LabelText("УТМ:")); top.Controls.Add(utmPick);
        top.Controls.Add(Primary("Обновить сертификаты", async () => await LoadCertificates(true)));
        top.Controls.Add(Button("Показать сохранённые", LoadSavedCertificates));
        root.Controls.Add(certGrid); root.Controls.Add(top); p.Controls.Add(root); return p;
    }

    TabPage BuildHistory()
    {
        var p = new TabPage("  История и события  ");
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var hp = new TabPage("Состояние УТМ"); hp.Controls.Add(historyGrid);
        var ep = new TabPage("События");
        var top = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 42, WrapContents = false };
        top.Controls.Add(Button("Обновить", () => { LoadHistory(); LoadEvents(); })); top.Controls.Add(Button("Экспорт событий", ExportEvents));
        ep.Controls.Add(eventGrid); ep.Controls.Add(top);
        tabs.TabPages.Add(hp); tabs.TabPages.Add(ep); p.Controls.Add(tabs); return p;
    }

    TabPage BuildLog()
    {
        var p = new TabPage("  Технический журнал  ");
        p.Controls.Add(log); return p;
    }

    static DataGridView Grid() => new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AutoGenerateColumns = true,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = Color.White, BorderStyle = BorderStyle.FixedSingle, RowHeadersVisible = false
    };

    static Label CardValue(string text) => new() { Text = text, AutoSize = false, Width = 150, Height = 38, Font = new("Segoe UI Semibold", 20F), TextAlign = ContentAlignment.MiddleLeft };

    static Control Card(string title, Label value)
    {
        var p = new Panel { Width = 175, Height = 88, BackColor = Color.White, Margin = new Padding(0, 0, 10, 0), Padding = new Padding(12) };
        p.Controls.Add(new Label { Text = title, AutoSize = true, ForeColor = Color.DimGray, Location = new(12, 9) }); value.Location = new(12, 30); p.Controls.Add(value); return p;
    }

    static Label LabelText(string text) => new() { Text = text, AutoSize = true, Padding = new Padding(0, 7, 5, 0) };

    Button Button(string text, Action action)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(10, 0, 10, 0) };
        b.Click += (_, _) => { try { action(); } catch (Exception ex) { Write(ex.ToString()); } }; return b;
    }

    Button Primary(string text, Func<Task> action)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 32, Padding = new Padding(12, 0, 12, 0) };
        b.Click += async (_, _) => { try { await action(); } catch (Exception ex) { Write(ex.ToString()); state.Text = "Ошибка"; } }; return b;
    }

    Button Primary(string text, Action action) => Button(text, action);

    void RefreshUtms()
    {
        var list = db.All();
        utmGrid.DataSource = list.Select(u => new
        {
            УТМ = u.Name,
            Адрес = $"{u.Host}:{u.Port}",
            Состояние = statuses.TryGetValue(u.Id, out var s) ? (s.Online ? "● В СЕТИ" : "● НЕТ СВЯЗИ") : "● НЕ ПРОВЕРЕН",
            Ответ = statuses.TryGetValue(u.Id, out var st) && st.HttpCode != 0 ? $"{st.HttpCode} / {st.ResponseMs} мс" : "—",
            Версия = statuses.TryGetValue(u.Id, out var sv) ? sv.Version ?? "—" : "—",
            ФСРАР = statuses.TryGetValue(u.Id, out var so) ? so.OwnerId ?? "—" : "—",
            Включён = u.Enabled ? "Да" : "Нет"
        }).ToList();
        utmPick.DataSource = null; utmPick.DataSource = list.ToList(); utmPick.DisplayMember = "Name"; utmPick.ValueMember = "Id";
        UpdateCards();
    }

    Utm? SelectedUtm()
    {
        if (utmPick.SelectedItem is Utm u) return u;
        var list = db.All();
        if (utmGrid.CurrentRow != null)
        {
            var name = utmGrid.CurrentRow.Cells[0].Value?.ToString();
            return list.FirstOrDefault(x => x.Name == name);
        }
        return list.FirstOrDefault();
    }

    void OnUtmChanged()
    {
        if (IsDisposed) return;
        LoadAllForSelected();
    }

    void LoadAllForSelected()
    {
        var u = SelectedUtm(); if (u == null) return;
        LoadDocuments(); LoadMarks(); LoadHistory(); LoadEvents(); LoadSavedCertificates(); UpdateCards();
    }

    async Task CheckAll()
    {
        if (busy) return;
        var list = db.All().Where(x => x.Enabled).ToList(); if (list.Count == 0) { state.Text = "Нет настроенных УТМ"; return; }
        busy = true; state.Text = "Проверяю связь…";
        try
        {
            var result = await Task.WhenAll(list.Select(async u => (u, s: await api.StatusAsync(u))));
            foreach (var x in result) { statuses[x.u.Id] = x.s; db.SaveStatus(x.u.Id, x.s); db.Event(x.u.Id, x.s.Online ? "INFO" : "ERROR", x.s.Online ? $"УТМ работает: {x.s.Version}" : $"УТМ недоступен: {x.s.Error}"); }
            RefreshUtms(); state.Text = $"Проверено: {result.Length} · {DateTime.Now:HH:mm:ss}"; LoadHistory(); UpdateCards();
        }
        finally { busy = false; }
    }

    async Task Sync(Utm u, bool showResult)
    {
        if (busy && showResult) return;
        if (showResult) state.Text = $"Синхронизирую «{u.Name}»…";
        try
        {
            var q = await api.OutQueueAsync(u);
            var incoming = await api.DocumentsAsync(u, "/api/db/in/list", "Входящие");
            var outgoing = await api.DocumentsAsync(u, "/api/db/out/list", "Исходящие");
            db.SaveDocuments(u.Id, incoming); db.SaveDocuments(u.Id, outgoing); db.SaveDocuments(u.Id, q);
            db.Event(u.Id, "INFO", $"Синхронизация: входящие {incoming.Count}, исходящие {outgoing.Count}, очередь {q.Count}");
            lastSync.Text = $"Последняя синхронизация: {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
            state.Text = $"Готово · документов: {incoming.Count + outgoing.Count + q.Count}";
            Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {u.Name}: входящие={incoming.Count}, исходящие={outgoing.Count}, очередь={q.Count}");
            LoadAllForSelected();
        }
        catch (Exception ex) { db.Event(u.Id, "ERROR", "Ошибка синхронизации", ex.ToString()); Write(ex.ToString()); state.Text = "Ошибка синхронизации"; }
    }

    async Task LoadDocumentsFromUtm() { var u = SelectedUtm(); if (u != null) await Sync(u, true); }

    void LoadDocuments()
    {
        var u = SelectedUtm(); if (u == null) return;
        var rows = db.Documents(u.Id).Select(x => new
        {
            ID = x.Id,
            Направление = FriendlyDirection(x.Direction),
            Документ = FriendlyType(x.Type),
            Номер = string.IsNullOrWhiteSpace(x.Number) ? "—" : x.Number,
            Дата = FriendlyDate(x.Date),
            Состояние = FriendlyStatus(x.Status),
            Позиций = x.Items
        }).ToList();
        docGrid.DataSource = rows; ApplyDocumentFilter();
    }

    void ApplyDocumentFilter()
    {
        var u = SelectedUtm(); if (u == null) return;
        var q = docSearch.Text.Trim();
        var rows = db.Documents(u.Id).Where(x => string.IsNullOrEmpty(q) || (x.Number + " " + x.Type + " " + x.Status + " " + x.Direction + " " + x.Id).Contains(q, StringComparison.OrdinalIgnoreCase)).Select(x => new
        {
            ID = x.Id, Направление = FriendlyDirection(x.Direction), Документ = FriendlyType(x.Type), Номер = string.IsNullOrWhiteSpace(x.Number) ? "—" : x.Number,
            Дата = FriendlyDate(x.Date), Состояние = FriendlyStatus(x.Status), Позиций = x.Items
        }).ToList();
        docGrid.DataSource = rows;
    }

    void OpenDocument()
    {
        if (docGrid.CurrentRow == null) { MessageBox.Show("Выберите документ.", "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
        var id = docGrid.CurrentRow.Cells[0].Value?.ToString(); var d = db.Documents(SelectedUtm()?.Id).FirstOrDefault(x => x.Id == id);
        if (d == null) return; using var f = new DocumentDetailsForm(d); f.ShowDialog(this);
    }

    async Task LoadMarks()
    {
        var u = SelectedUtm(); if (u == null) return;
        markGrid.DataSource = db.Marks(u.Id).Select(x => new { ID = x.Id, Type = x.Type, Rank = x.Rank, Номер = x.Number, Состояние = FriendlyMarkStatus(x.Status), Проверено = FriendlyDate(x.CheckedAt), Код = Short(x.Raw, 80) }).ToList();
        ApplyMarkFilter(); await Task.CompletedTask;
    }

    void ApplyMarkFilter()
    {
        var u = SelectedUtm(); if (u == null) return; var q = markSearch.Text.Trim();
        markGrid.DataSource = db.Marks(u.Id).Where(x => string.IsNullOrEmpty(q) || (x.Raw + x.Type + x.Rank + x.Number + x.Status).Contains(q, StringComparison.OrdinalIgnoreCase)).Select(x => new { ID = x.Id, Type = x.Type, Rank = x.Rank, Номер = x.Number, Состояние = FriendlyMarkStatus(x.Status), Проверено = FriendlyDate(x.CheckedAt), Код = Short(x.Raw, 80) }).ToList();
    }

    async Task LoadCertificates(bool refresh)
    {
        var u = SelectedUtm(); if (u == null) return;
        if (refresh) { state.Text = "Получаю сертификаты…"; var a = await api.CertificatesAsync(u); db.SaveCertificates(u.Id, a); db.Event(u.Id, "INFO", $"Сертификатов получено: {a.Count}"); }
        ShowCertificates(db.Certificates(u.Id)); UpdateCards(); state.Text = "Готово";
    }

    void LoadSavedCertificates() { var u = SelectedUtm(); if (u != null) ShowCertificates(db.Certificates(u.Id)); }
    void ShowCertificates(IEnumerable<CertificateInfo> a) => certGrid.DataSource = a.Select(x => new { Тип = FriendlyCert(x.Name), Владелец = Short(x.Subject, 90), Удостоверяющий = Short(x.Issuer, 70), ДействуетДо = FriendlyDate(x.NotAfter), Состояние = x.Valid ? "Действует" : "Недействителен" }).ToList();
    void LoadHistory() { var u = SelectedUtm(); historyGrid.DataSource = u == null ? Array.Empty<object>() : db.History(u.Id).Select(x => new { Время = FriendlyDate(x.CheckedAt), Состояние = x.Online ? "В СЕТИ" : "НЕТ СВЯЗИ", HTTP = x.HttpCode, Ответ = $"{x.ResponseMs} мс", Версия = x.Version, Ошибка = x.Error }).ToList(); }
    void LoadEvents() { var u = SelectedUtm(); eventGrid.DataSource = db.Events(u?.Id).Select(x => new { Время = FriendlyDate(x.CreatedAt), Уровень = FriendlyLevel(x.Level), Событие = x.Message }).ToList(); }

    void UpdateCards()
    {
        var all = db.All(); var selected = SelectedUtm();
        cardUtms.Text = all.Count.ToString(); cardOnline.Text = statuses.Values.Count(x => x.Online).ToString();
        var docs = selected == null ? 0 : db.Documents(selected.Id).Count; var marks = selected == null ? 0 : db.Marks(selected.Id).Count; var certs = selected == null ? 0 : db.Certificates(selected.Id).Count;
        cardDocs.Text = docs.ToString("N0"); cardMarks.Text = marks.ToString("N0"); cardCertificates.Text = certs.ToString();
        var attention = all.Count(x => statuses.TryGetValue(x.Id, out var s) && !s.Online);
        if (selected != null) attention += db.Certificates(selected.Id).Count(x => !x.Valid);
        cardAttention.Text = attention.ToString();
    }

    void Add() { using var f = new EditForm(); if (f.ShowDialog(this) == DialogResult.OK) { if (string.IsNullOrWhiteSpace(f.N) || string.IsNullOrWhiteSpace(f.H)) { MessageBox.Show("Заполните название и адрес УТМ."); return; } db.Add(f.N, f.H, f.P); RefreshUtms(); } }
    void Edit() { var u = SelectedUtm(); if (u == null) return; using var f = new EditForm(u); if (f.ShowDialog(this) == DialogResult.OK) { db.Update(u, f.N, f.H, f.P, f.E); RefreshUtms(); } }
    void Delete() { var u = SelectedUtm(); if (u == null) return; if (MessageBox.Show($"Удалить УТМ «{u.Name}» и его сохранённую историю?", "Удаление", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) { db.Delete(u.Id); statuses.Remove(u.Id); RefreshUtms(); } }
    void OpenDataFolder() { try { Process.Start(new ProcessStartInfo("explorer.exe", db.DataDirectory) { UseShellExecute = true }); } catch (Exception ex) { Write(ex.ToString()); } }

    void ExportDocuments() => ExportCsv("documents.csv", new[] { "ID", "Direction", "Type", "Number", "Date", "Status", "Items" }, db.Documents(SelectedUtm()?.Id).Select(x => new[] { x.Id, x.Direction, x.Type, x.Number, x.Date, x.Status, x.Items.ToString() }));
    void ExportMarks() => ExportCsv("marks.csv", new[] { "ID", "Document", "Type", "Rank", "Number", "Status", "CheckedAt", "Raw" }, db.Marks(SelectedUtm()?.Id).Select(x => new[] { x.Id.ToString(), x.DocumentId, x.Type, x.Rank, x.Number, x.Status, x.CheckedAt, x.Raw }));
    void ExportEvents() => ExportCsv("events.csv", new[] { "CreatedAt", "Level", "Message", "Raw" }, db.Events(SelectedUtm()?.Id).Select(x => new[] { x.CreatedAt, x.Level, x.Message, x.Raw }));
    void ExportCsv(string name, string[] header, IEnumerable<string[]> rows) { using var s = new SaveFileDialog { FileName = name, Filter = "CSV UTF-8|*.csv" }; if (s.ShowDialog(this) != DialogResult.OK) return; var sb = new StringBuilder(); sb.AppendLine(string.Join(';', header.Select(Csv))); foreach (var r in rows) sb.AppendLine(string.Join(';', r.Select(Csv))); File.WriteAllText(s.FileName, sb.ToString(), new UTF8Encoding(true)); MessageBox.Show("Экспорт завершён.", "UTM Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information); }
    static string Csv(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";
    void Write(string s) { if (IsDisposed) return; try { BeginInvoke(() => log.AppendText(s + Environment.NewLine)); } catch { } }

    static string FriendlyDirection(string s) => s switch { "Входящие" => "Входящий документ", "Исходящие" => "Исходящий документ", _ when s.Contains("in", StringComparison.OrdinalIgnoreCase) => "Входящий документ", _ when s.Contains("out", StringComparison.OrdinalIgnoreCase) => "Исходящий документ", _ => string.IsNullOrWhiteSpace(s) ? "Документ" : s };
    static string FriendlyType(string s) { if (string.IsNullOrWhiteSpace(s)) return "Документ ЕГАИС"; if (s.Contains("WayBill", StringComparison.OrdinalIgnoreCase)) return "Товарно-транспортная накладная (ТТН)"; if (s.Contains("Act", StringComparison.OrdinalIgnoreCase)) return "Акт ЕГАИС"; if (s.Contains("Reply", StringComparison.OrdinalIgnoreCase)) return "Ответ ЕГАИС"; return s.Replace("_", " "); }
    static string FriendlyStatus(string s) { if (string.IsNullOrWhiteSpace(s)) return "Не указан"; if (s.Contains("accept", StringComparison.OrdinalIgnoreCase) || s.Contains("accepted", StringComparison.OrdinalIgnoreCase)) return "Принят"; if (s.Contains("reject", StringComparison.OrdinalIgnoreCase)) return "Отклонён"; if (s.Contains("done", StringComparison.OrdinalIgnoreCase) || s.Contains("complete", StringComparison.OrdinalIgnoreCase)) return "Обработан"; if (s.Contains("error", StringComparison.OrdinalIgnoreCase)) return "Ошибка"; return s; }
    static string FriendlyMarkStatus(string s) => string.IsNullOrWhiteSpace(s) ? "Не проверялась" : s.Equals("OK", StringComparison.OrdinalIgnoreCase) ? "Проверка успешна" : s.Equals("TIMEOUT", StringComparison.OrdinalIgnoreCase) ? "Нет ответа УТМ" : s;
    static string FriendlyCert(string s) => s.Contains("GOST", StringComparison.OrdinalIgnoreCase) ? "ГОСТ" : s.Contains("RSA", StringComparison.OrdinalIgnoreCase) ? "RSA" : s;
    static string FriendlyLevel(string s) => s.Equals("ERROR", StringComparison.OrdinalIgnoreCase) ? "ОШИБКА" : s.Equals("WARN", StringComparison.OrdinalIgnoreCase) ? "ВНИМАНИЕ" : "ИНФО";
    static string FriendlyDate(string s) { if (DateTime.TryParse(s, out var d)) return d.ToLocalTime().ToString("dd.MM.yyyy HH:mm:ss"); return s; }
    static string Short(string? s, int n) { s ??= ""; return s.Length <= n ? s : s[..n] + "…"; }
    static MarkParts? ParseMark(string raw) { if (string.IsNullOrWhiteSpace(raw)) return null; var clean = raw.Replace("\u001d", ""); var m = Regex.Match(clean, @"(?<type>\d{3})[^0-9]{0,3}(?<rank>\d{3})[^0-9]{0,3}(?<num>\d{8})"); if (m.Success) return new(m.Groups["type"].Value, m.Groups["rank"].Value, m.Groups["num"].Value, raw); var digits = new string(clean.Where(char.IsDigit).ToArray()); return digits.Length >= 14 ? new(digits[..3], digits.Substring(3, 3), digits.Substring(6, 8), raw) : null; }

    protected override void OnFormClosing(FormClosingEventArgs e) { api.Dispose(); base.OnFormClosing(e); }
}

public sealed class EditForm : Form
{
    readonly TextBox n = new(), h = new();
    readonly NumericUpDown p = new() { Minimum = 1, Maximum = 65535, Value = 8080 };
    readonly CheckBox en = new() { Text = "УТМ включён для мониторинга", Checked = true, AutoSize = true };
    public string N => n.Text.Trim(); public string H => h.Text.Trim(); public int P => (int)p.Value; public bool E => en.Checked;
    public EditForm(Utm? u = null)
    {
        Text = u == null ? "Добавить УТМ" : "Настройка УТМ"; Width = 470; Height = 285; FormBorderStyle = FormBorderStyle.FixedDialog; StartPosition = FormStartPosition.CenterParent; MaximizeBox = false; MinimizeBox = false; Font = new("Segoe UI", 9.5F);
        var ok = new Button { Text = "Сохранить", DialogResult = DialogResult.OK, Height = 38, Dock = DockStyle.Bottom };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel, Height = 38, Dock = DockStyle.Bottom };
        var t = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(16) }; t.ColumnStyles.Add(new(SizeType.Absolute, 130)); t.ColumnStyles.Add(new(SizeType.Percent, 100));
        n.Text = u?.Name ?? "УТМ"; h.Text = u?.Host ?? "127.0.0.1"; p.Value = u?.Port ?? 8080; en.Checked = u?.Enabled ?? true;
        t.Controls.Add(new Label { Text = "Название", AutoSize = true }, 0, 0); t.Controls.Add(n, 1, 0); t.Controls.Add(new Label { Text = "Адрес / IP", AutoSize = true }, 0, 1); t.Controls.Add(h, 1, 1); t.Controls.Add(new Label { Text = "Порт УТМ", AutoSize = true }, 0, 2); t.Controls.Add(p, 1, 2); t.Controls.Add(en, 1, 3);
        var tip = new Label { Text = "Например: 10.0.0.100 и порт 8086", AutoSize = true, ForeColor = Color.DimGray }; t.Controls.Add(tip, 1, 4);
        Controls.Add(t); Controls.Add(cancel); Controls.Add(ok); AcceptButton = ok; CancelButton = cancel;
    }
}
