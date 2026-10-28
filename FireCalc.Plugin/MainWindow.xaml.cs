using System.Globalization;
using System.Windows;
using FireCalc.Core;
using Microsoft.Win32;

namespace FireCalc.Plugin;

public partial class MainWindow : Window
{
    private readonly ModelBridge _bridge;
    private readonly List<HydraulicUiRow> _hydroRows = new();
    private HydraulicRouteResult? _hydroResult;
    private RouteFixture? _hydroFixture;
    private string _hydroScenario = "Источник не выбран";
    private List<PipeSegment> _pipes = new();
    private ReserveInputs? _inputs;
    private ReserveResults? _results;
    private string _example = "Окрасочный цех (ЭФЭР)";
    private readonly List<MetricRow> _metrics = new();
    private static readonly CultureInfo Ru = CultureInfo.GetCultureInfo("ru-RU");

    public MainWindow(ModelBridge bridge)
    {
        InitializeComponent();
        _bridge = bridge;
        InitializeHelp();
        ApplyPainting();
        CalculateNow();
        LoadHydroFixture(EferHydraulicFixtures.Painting());
    }

    // Fit the working area (including Windows taskbar and display scaling).
    // WPF uses device-independent units, so SystemParameters.WorkArea is the appropriate bound.
    private void HelpSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (HelpGrid is null || HelpSearchBox is null) return;
        var text = HelpSearchBox.Text.Trim();
        HelpGrid.ItemsSource = HelpCatalog.All
            .Where(k => text.Length == 0 || k.Key.Contains(text, StringComparison.OrdinalIgnoreCase)
                || k.Value.Contains(text, StringComparison.OrdinalIgnoreCase))
            .OrderBy(k=>k.Key).ToList();
    }

    private void InitializeHelp()
    {
        HelpGrid.ItemsSource = HelpCatalog.All.OrderBy(k=>k.Key).ToList();
        var fields = new (System.Windows.Controls.TextBox TextBox, string Key)[]
        {
            (SuppressionMinutesBox,"Время тушения, мин"),
            (SolutionFlowBox,"Расход раствора ПО, л/с"),
            (ConcentrationBox,"Концентрация ПО, %"),
            (CoolingMinutesBox,"Время охлаждения, мин"),
            (CoolingFlowBox,"Расход охлаждения, л/с"),
            (FoamPipeVolumeBox,"Wтр — объём труб ПО, м³"),
            (DryPipeVolumeBox,"Объём сухотруба, м³"),
            (TankVolumeBox,"Ёмкость бака-дозатора, м³"),
            (HydroLocalBox,"Местные потери, %"),
            (HydroEquipmentBox,"Потери оборудования, м"),
            (HydroConsumerBox,"Напор потребителя, м"),
            (HydroElevationBox,"Перепад отметок, м"),
            (NetworkStartBox,"ID начального объекта"),
            (NetworkEndBox,"ID конечного объекта"),
            (NetworkRootBox,"ID источника"),
            (NetworkDemandsBox,"Потребители: ID=Q л/с"),
        };
        foreach (var (field, key) in fields)
        {
            field.ToolTip = HelpCatalog.Get(key);
            System.Windows.Controls.ToolTipService.SetShowDuration(field, 24000);
        }
        HydroSummaryText.ToolTip = "Hтреб = Σhлин + hмест + hобор + Hпотр + Δz; для реального насоса дополнительно требуются паспорта и проверка режима.";
        HydroReferenceText.ToolTip = "Коэффициенты, направления, потери оборудования и выделение пути требуют подтверждения инженера.";
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var area = SystemParameters.WorkArea;
        MaxWidth = Math.Max(MinWidth, area.Width - 12);
        MaxHeight = Math.Max(MinHeight, area.Height - 12);
        Width = Math.Min(Width, MaxWidth);
        Height = Math.Min(Height, MaxHeight);
        Left = area.Left + Math.Max(0, (area.Width - Width) / 2);
        Top = area.Top + Math.Max(0, (area.Height - Height) / 2);
    }

    private static double Parse(string text, string name)
    {
        if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException($"Заполните поле «{name}»");
        if (double.TryParse(text.Trim(), NumberStyles.Float, Ru, out var v) ||
            double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
        throw new ArgumentException($"Неверное число: {name}");
    }

    private ReserveInputs ReadInputs() => new(
        Parse(SuppressionMinutesBox.Text, "Время тушения"),
        Parse(SolutionFlowBox.Text, "Расход раствора"),
        Parse(ConcentrationBox.Text, "Концентрация"),
        Parse(CoolingMinutesBox.Text, "Время охлаждения"),
        Parse(CoolingFlowBox.Text, "Расход охлаждения"),
        Parse(FoamPipeVolumeBox.Text, "Wтр"),
        Parse(DryPipeVolumeBox.Text, "Объём сухотруба"),
        string.IsNullOrWhiteSpace(TankVolumeBox.Text) ? null : Parse(TankVolumeBox.Text, "Ёмкость бака"));

    private static string N(double x) => x.ToString("0.###", Ru);
    private static string NV(double x) => x.ToString("0.00000000", Ru);

    private void Add(string label, double number) => _metrics.Add(new MetricRow(label, N(number), "м³"));
    private void CalculateNow()
    {
        try
        {
            _inputs = ReadInputs();
            _results = ReserveCalculator.Calculate(_inputs);
            _metrics.Clear();
            Add("Вода на тушение, Vт", _results.SuppressionWaterM3);
            Add("Вода на охлаждение, Vо", _results.CoolingWaterM3);
            Add("Вода, всего (Excel V)", _results.TotalWaterM3);
            Add("Вода + объём сухотруба (доп. показатель)", _results.WaterWithDryPipeM3);
            Add("Рабочий запас ПО, Wраб", _results.WorkingFoamM3);
            Add("Резерв ПО, Wрез", _results.ReserveFoamM3);
            Add("Поправка дозирования, Wкомп", _results.DosingCorrectionFoamM3);
            Add("Испытания, Wисп", _results.TestFoamM3);
            Add("Вместимость труб (Wтр)", _results.FoamPipesM3);
            Add("Минимальный уровень (Wmin)", _results.MinimumLevelFoamM3);
            Add("Общий запас ПО, W", _results.TotalFoamM3);
            if (_results.TankCapacityM3.HasValue)
            {
                Add("Ёмкость бака", _results.TankCapacityM3.Value);
                _metrics.Add(new MetricRow("Проверка по расчётному объёму", _results.TankSufficient == true ? "Достаточно" : "Недостаточно", ""));
            }
            MetricsGrid.ItemsSource = null;
            MetricsGrid.ItemsSource = _metrics.ToList();
            ReserveExportButton.IsEnabled = true;
            StatusText.Text = "Расчёт выполнен локально по формулам Excel. Инженерная проверка обязательна.";
            Log($"Расчёт «{_example}»: вода {N(_results.TotalWaterM3)} м³, ПО {N(_results.TotalFoamM3)} м³.");
        }
        catch (Exception ex)
        {
            _results = null;
            ReserveExportButton.IsEnabled = false;
            MessageBox.Show(ex.Message, "FireCalc — входные данные", MessageBoxButton.OK, MessageBoxImage.Warning);
            StatusText.Text = "Ошибка входных данных: " + ex.Message;
        }
    }

    private void Log(string text) => LogBox.AppendText($"{DateTime.Now:HH:mm:ss}  {text}\n");
    private void ReserveInputs_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _inputs = null;
        _results = null;
        if (ReserveExportButton is not null) ReserveExportButton.IsEnabled = false;
    }

    private void Calculate_Click(object sender, RoutedEventArgs e) => CalculateNow();

    private void ApplyPainting()
    {
        _example = "ЭФЭР: окрасочный цех";
        SetValues(15, 80, 1, 0, 0, 0.14, 13.84, 2.5);
        StatusText.Text = "ДЕМО: окрасочный цех. Числа из РУП.Т; расчёт Excel отличается от цифр PDF.";
    }
    private void ApplyRepair()
    {
        _example = "ЭФЭР: ремонтные цеха";
        SetValues(10, 80, 1, 60, 80, 0.25, 24.13, 2.5);
        StatusText.Text = "ДЕМО: ремонтные цеха. Числа из РУП.Т; расчёт Excel отличается от цифр PDF.";
    }
    private void SetValues(double a,double b,double c,double d,double e,double f,double g,double h)
    {
        SuppressionMinutesBox.Text = N(a); SolutionFlowBox.Text=N(b); ConcentrationBox.Text=N(c);
        CoolingMinutesBox.Text=N(d); CoolingFlowBox.Text=N(e); FoamPipeVolumeBox.Text=N(f);
        DryPipeVolumeBox.Text=N(g); TankVolumeBox.Text=N(h);
    }
    private void Painting_Click(object sender, RoutedEventArgs e) { ApplyPainting(); CalculateNow(); }
    private void Repair_Click(object sender, RoutedEventArgs e) { ApplyRepair(); CalculateNow(); }

    private void ReadPipes_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _pipes = _bridge.ReadPipes();
            PipesGrid.ItemsSource = null;
            PipesGrid.ItemsSource = _pipes;
            int complete = _pipes.Count(p=>p.InternalVolumeM3.HasValue);
            var volume = _pipes.Sum(p=>p.InternalVolumeM3 ?? 0);
            StatusText.Text = $"В модели найдено труб: {_pipes.Count}; с вычислимым объёмом: {complete}. Сумма объёмов: {NV(volume)} м³ (не привязана к расчётной системе).";
            Log($"Renga: {_pipes.Count} труб; {complete} полных; объём {NV(volume)} м³. Неизвестная трассировка системы!");
        }
        catch (Exception ex) { MessageBox.Show("Ошибка чтения модели:\n"+ex.Message,"Renga",MessageBoxButton.OK,MessageBoxImage.Error); }
    }

    private void UsePipes_Click(object sender, RoutedEventArgs e)
    {
        if (_pipes.Count == 0) { MessageBox.Show("Сначала прочитайте модель."); return; }
        int valid = _pipes.Count(p=>p.InternalVolumeM3.HasValue);
        if (valid != _pipes.Count)
        {
            MessageBox.Show("Не все трубы имеют длину и внутренний диаметр. Вносить неполный объём опасно.\nИзучите вкладку диагностики.");
            return;
        }
        var sum = _pipes.Sum(p=>p.InternalVolumeM3 ?? 0);
        var question = $"Найдены {_pipes.Count} труб, суммарный объём {NV(sum)} м³.\n\n"+
            "Подтверждаете, что ВСЕ эти трубы относятся к одному расчётному сухотрубу?\n"+
            "Этот объём НЕ будет автоматически занесён в Wтр для пенообразователя.";
        if (MessageBox.Show(question,"Подтверждение расчётной системы",MessageBoxButton.YesNo,MessageBoxImage.Warning)==MessageBoxResult.Yes)
        {
            DryPipeVolumeBox.Text = NV(sum);
            Log("Пользователь вручную подтвердил объём сухотруба всех найденных труб.");
            CalculateNow();
        }
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        if (_results is null || _inputs is null) { MessageBox.Show("Сначала выполните расчёт."); return; }
        var dialog = new SaveFileDialog {Filter="Excel (*.xlsx)|*.xlsx",FileName="FireCalc_EFER_Расчет.xlsx",DefaultExt=".xlsx"};
        if(dialog.ShowDialog(this)!=true)return;
        try
        {
            ExcelExporter.Write(dialog.FileName,_example,_inputs,_metrics,_pipes);
            Log("Создан Excel: "+dialog.FileName);
            StatusText.Text = "Расчёт экспортирован в XLSX: "+dialog.FileName;
        }
        catch (Exception ex) { MessageBox.Show(ex.Message,"Ошибка XLSX",MessageBoxButton.OK,MessageBoxImage.Error); }
    }

    private static bool SameNumber(double? a, double? b)
    {
        if (!a.HasValue || !b.HasValue) return a.HasValue == b.HasValue;
        return Math.Abs(a.Value - b.Value) <= 0.000001;
    }

    private void Write_Click(object sender, RoutedEventArgs e)
    {
        if (_pipes.Count == 0) { MessageBox.Show("Прочитайте трубы Renga."); return; }
        if (MessageBox.Show("Записать рассчитанный геометрический объём в пользовательское свойство каждой трубы с полными данными?\n"+
            "Сохраните копию проекта перед первым использованием.","Изменение модели",MessageBoxButton.YesNo,MessageBoxImage.Warning)!=MessageBoxResult.Yes)return;
        try
        {
            // Guard against overwriting stale object IDs when model geometry or project changed.
            var currentPipes = _bridge.ReadPipes();
            var previous = _pipes.ToDictionary(p => p.RengaObjectId);
            if (currentPipes.Count != previous.Count || currentPipes.Any(p =>
                !previous.TryGetValue(p.RengaObjectId, out var old) ||
                !SameNumber(p.LengthM, old.LengthM) ||
                !SameNumber(p.InnerDiameterMm, old.InnerDiameterMm)))
                throw new InvalidOperationException("Модель изменилась после чтения труб. Нажмите «Обновить трубы из Renga», проверьте параметры и повторите запись.");
            int changed = _bridge.WritePipeVolumes(currentPipes);
            _pipes = currentPipes;
            Log($"Записан геометрический объём для {changed} труб в свойство FireCalc.");
            StatusText.Text=$"Записано свойств труб: {changed}. Пересчитайте/настройте спецификацию Renga вручную.";
        }
        catch(Exception ex){ MessageBox.Show("Запись не выполнена:\n"+ex.Message,"Renga",MessageBoxButton.OK,MessageBoxImage.Error); }
    }

    // Hydraulic route v0.2: the user MUST identify the dictating path.
    // These rows are disconnected from automatic network flow balancing by design.
    public sealed class HydraulicUiRow
    {
        public bool Included { get; set; }
        public string Name { get; set; } = "";
        public int? RengaId { get; set; }
        public double? LengthM { get; set; }
        public double? DiameterMm { get; set; }
        public double? FlowLs { get; set; }
        public double? Kt { get; set; }
        public double? LinearLossMeters { get; set; }
        public double? VelocityMs { get; set; }
    }

    private void SetHydroRows(IEnumerable<HydraulicUiRow> rows)
    {
        _hydroRows.Clear(); _hydroRows.AddRange(rows);
        _hydroResult = null;
        HydroGrid.ItemsSource = null;
        HydroGrid.ItemsSource = _hydroRows;
        HydroSummaryText.Text = $"Найдено участков: {_hydroRows.Count}. Отметьте одну непрерывную расчётную трассу и задайте параметры.";
        HydroExportButton.IsEnabled = false;
    }

    private void HydroFromModel_Click(object sender, RoutedEventArgs e)
    {
        // IMPORTANT: Never inherit equipment/head/elevation values from the EFER demo.
        _hydroFixture = null;
        _hydroResult = null;
        _hydroScenario = "Renga — введённый вручную расчётный путь";
        HydroLocalBox.Text = "0";
        HydroEquipmentBox.Text = "0";
        HydroConsumerBox.Text = "0";
        HydroElevationBox.Text = "0";
        HydroConfirmationBox.IsChecked = false;
        HydroExportButton.IsEnabled = false;
        HydroScenarioText.Text = "Режим: трубы из текущей BIM-модели (не демо ЭФЭР)";
        HydroGuidanceText.Text = "По модели получаем длину и внутренний диаметр. Q, Кт, выбранный путь, потери оборудования и напор потребителя должны быть заданы инженером; нули здесь означают только пустую исходную настройку.";
        HydroReferenceText.Text = "Расчёт ещё не подтверждён. Нельзя применять коэффициенты стальных труб ЭФЭР к PPR/ВЧШГ без расчётного обоснования.";
        SetHydroRows(Array.Empty<HydraulicUiRow>());
        _pipes = new List<PipeSegment>();
        PipesGrid.ItemsSource = null;
        try
        {
            _pipes = _bridge.ReadPipes();
            PipesGrid.ItemsSource = null;
            PipesGrid.ItemsSource = _pipes;
            SetHydroRows(_pipes.Select(p => new HydraulicUiRow {
                Included = false, RengaId = p.RengaObjectId, Name = p.Name,
                LengthM = p.LengthM, DiameterMm = p.InnerDiameterMm,
                FlowLs = null, Kt = null }));
            StatusText.Text = $"Получено труб из Renga: {_pipes.Count}. В гидравлике отметьте маршрут и заполните Q/Кт, затем подтвердите данные.";
            Log($"Гидравлика: считано {_hydroRows.Count} труб, демопараметры сброшены. Трасса не определена.");
        }
        catch (Exception ex)
        {
            HydroSummaryText.Text = "Не удалось загрузить трубы из Renga.";
            MessageBox.Show(ex.Message, "FireCalc — чтение модели", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void LoadHydroFixture(RouteFixture fixture)
    {
        _hydroFixture = fixture;
        _hydroScenario = fixture.Name;
        HydroLocalBox.Text = N(fixture.Settings.LocalLossPercent);
        HydroEquipmentBox.Text = N(fixture.Settings.EquipmentLossMeters);
        HydroConsumerBox.Text = N(fixture.Settings.RequiredConsumerHeadMeters);
        HydroElevationBox.Text = N(fixture.Settings.ElevationDifferenceMeters);
        HydroConfirmationBox.IsChecked = true; // Confirmed fixture, NOT a user-validated route.
        HydroScenarioText.Text = $"Режим: демонстрация — {fixture.Name}";
        HydroGuidanceText.Text = "Тестовый пример из документа ЭФЭР. Выбранные участки не доказаны как непрерывная трасса; перепроверка инженером необходима.";
        SetHydroRows(fixture.Lines.Select(p => new HydraulicUiRow {
            Included = p.Included, Name = p.Name, LengthM = p.LengthM,
            FlowLs = p.FlowLs, Kt = p.Kt }));
        HydroReferenceText.Text = "Числа по РУП.Т, стр. 21–22. Участки для демонстрации выбраны по итоговым потерям; реальная связность трассы не установлена. Итоги PDF содержат расхождения.";
        CalculateHydro();
    }

    private void HydroPainting_Click(object sender, RoutedEventArgs e) => LoadHydroFixture(EferHydraulicFixtures.Painting());
    private void HydroRepair_Click(object sender, RoutedEventArgs e) => LoadHydroFixture(EferHydraulicFixtures.Repair());
    private void HydroCalculate_Click(object sender, RoutedEventArgs e) => CalculateHydro();

    private void CalculateHydro()
    {
        try
        {
            _hydroResult = null;
            HydroExportButton.IsEnabled = false;
            if (_hydroFixture is null && HydroConfirmationBox.IsChecked != true)
                throw new ArgumentException("Для расчёта труб Renga сначала подтвердите выбор пути и введённые Q/Кт (флажок над таблицей). Демоданные из ЭФЭР не используются.");

            HydroGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
            HydroGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
            var selected = _hydroRows.Where(p => p.Included).ToList();
            var sections = new List<RouteSection>(selected.Count);
            foreach (var s in selected)
            {
                if (s.LengthM is null || s.FlowLs is null || s.Kt is null)
                    throw new ArgumentException($"Участок «{s.Name}»: обязательны длина, Q и Кт.");
                sections.Add(new RouteSection(s.Name, s.LengthM.Value, s.FlowLs.Value, s.Kt.Value,
                    s.DiameterMm));
            }
            var settings = new RouteSettings(
                Parse(HydroLocalBox.Text, "Местные потери"),
                Parse(HydroEquipmentBox.Text, "Потери оборудования"),
                Parse(HydroConsumerBox.Text, "Напор потребителя"),
                Parse(HydroElevationBox.Text, "Перепад отметок"));
            _hydroResult = HydraulicRouteCalculator.Calculate(sections, settings);
            foreach (var row in _hydroRows) { row.LinearLossMeters = null; row.VelocityMs = null; }
            var speedWarnings = new List<string>();
            for (int i = 0; i < selected.Count; i++)
            {
                selected[i].LinearLossMeters = _hydroResult.Sections[i].LinearLossMeters;
                if (_hydroResult.Sections[i].VelocityMs is double velocity)
                {
                    selected[i].VelocityMs = velocity;
                    if (velocity > 3.0)
                        speedWarnings.Add($"{selected[i].Name}: v={selected[i].VelocityMs:F2} м/с");
                }
            }
            HydroGrid.Items.Refresh();
            if (speedWarnings.Count != 0)
                Log("Скорость выше ориентировочных 3 м/с (НЕ нормативный предел): " +
                    string.Join("; ", speedWarnings));
            var r = _hydroResult;
            HydroSummaryText.Text = $"Выбранная трасса: {r.Sections.Count} участков, L={N(r.LengthMeters)} м. " +
                $"Линейные {r.LinearLossMeters:F3} м + местные {r.LocalLossMeters:F3} м + " +
                $"оборудование {r.EquipmentLossMeters:F3} м = {r.TotalCircuitLossMeters:F3} м. " +
                (_hydroFixture is null
                    ? $"УСЛОВНЫЙ ИТОГ ВВЕДЁННЫХ ДАННЫХ: {r.RequiredSourceHeadMeters:F3} м (не напор всей установки)."
                    : $"Расчётный напор по демосценарию: {r.RequiredSourceHeadMeters:F3} м.");
            HydroExportButton.IsEnabled = true;
            if (_hydroFixture is null)
                HydroReferenceText.Text = "Все Q/Кт и потери введены вручную. Связность маршрута и подбор насоса не проверены. Нулевые потери оборудования или напор потребителя не означают, что их нет.";
            else
                HydroReferenceText.Text = $"Сверка с PDF: потери трассы {_hydroFixture.PdfCircuitLossMeters:F3} м, расчёт {r.TotalCircuitLossMeters:F3} м; " +
                    $"напор в PDF {_hydroFixture.PdfRequiredHeadMeters:F3} м, расчёт {r.RequiredSourceHeadMeters:F3} м. " +
                    "Разницу в суммарном напоре не скрываем: исходная документация требует уточнения.";
            if (speedWarnings.Count != 0)
                HydroReferenceText.Text += " ВНИМАНИЕ: скорость в некоторых трубах выше ориентировочных 3 м/с. Это диагностический порог, а не нормативный предел. Проверьте применимость методики.";
            StatusText.Text = "Гидравлический расчёт вручную выбранной трассы выполнен. Не является автоматическим расчётом сети.";
            Log($"Гидравлика «{_hydroScenario}»: {_hydroResult.Sections.Count} участков, напор {r.RequiredSourceHeadMeters:F3} м.");
        }
        catch (Exception ex)
        {
            _hydroResult = null;
            HydroExportButton.IsEnabled = false;
            HydroSummaryText.Text = "Расчёт не выполнен: " + ex.Message;
            MessageBox.Show(ex.Message, "FireCalc — гидравлический расчёт", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void HydroSettings_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        InvalidateHydraulics();
    }

    private void HydroGrid_CellEditEnding(object sender, System.Windows.Controls.DataGridCellEditEndingEventArgs e)
    {
        InvalidateHydraulics();
    }

    private void HydroConfirmation_Changed(object sender, RoutedEventArgs e)
    {
        InvalidateHydraulics();
    }

    private void InvalidateHydraulics()
    {
        // During InitializeComponent() named controls can still be null.
        _hydroResult = null;
        if (HydroExportButton is not null) HydroExportButton.IsEnabled = false;
        if (HydroSummaryText is not null)
            HydroSummaryText.Text = "Данные изменены. Нажмите «Рассчитать выбранную трассу» для обновления результата.";
    }

    private void HydroExport_Click(object sender, RoutedEventArgs e)
    {
        if (_hydroResult is null || (_hydroFixture is null && HydroConfirmationBox.IsChecked != true))
        {
            MessageBox.Show("Сначала подтвердите путь и выполните расчёт выбранной трассы.");
            return;
        }
        var dialog = new SaveFileDialog { Filter = "Excel (*.xlsx)|*.xlsx", FileName = "FireCalc_EFER_Гидравлика.xlsx", DefaultExt = ".xlsx" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            ExcelExporter.WriteHydraulic(dialog.FileName, _hydroScenario, _hydroResult);
            Log("Экспорт гидравлики: " + dialog.FileName);
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "FireCalc — экспорт гидравлики", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    public sealed record MetricRow(string Name, string Value, string Units)
    {
        public string Help => HelpCatalog.Get(Name);
    }
}
