using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Windows;
using FireCalc.Core;

namespace FireCalc.Plugin;

public partial class MainWindow
{
    private NetworkTopology? _networkTopology;
    private TreeFlowResult? _treeFlows;

    public sealed record RouteDisplay(int RouteId, int SourceObjectId,
        int TargetObjectId, int SystemStyleId, double? DerivedFlowLs, int PipeCount, string PipeIds);

    private static string NetworkSignature(IEnumerable<RouteLink> links) =>
        string.Join("|", links.OrderBy(x=>x.RouteId).Select(r =>
            $"{r.RouteId}/{r.SourceObjectId}/{r.TargetObjectId}/{r.SystemStyleId}/{string.Join(",", r.PipeObjectIds)}"));

    private void BindRouteRows()
    {
        if (_networkTopology is null) return;
        NetworkGrid.ItemsSource = _networkTopology.Routes.Select(e => new RouteDisplay(e.RouteId,
            e.SourceObjectId, e.TargetObjectId, e.SystemStyleId,
            _treeFlows != null && _treeFlows.RouteFlowLs.TryGetValue(e.RouteId,out var q) ? q : null,
            e.PipeObjectIds.Count, string.Join(", ", e.PipeObjectIds))).ToList();
    }

    private void NetworkScan_Click(object sender, RoutedEventArgs e)
    {
        _networkTopology = null;
        _treeFlows = null;
        NetworkGrid.ItemsSource = null;
        NetworkFlowText.Text = "Введите подтверждённые расходы потребителей для древовидной сети.";
        NetworkPathText.Text = "Выберите ID двух концевых объектов из таблицы.";
        try
        {
            var (links, messages) = _bridge.ReadRouteNetwork();
            _networkTopology = new NetworkTopology(links);
            BindRouteRows();
            var groups = _networkTopology.Components();
            NetworkSummaryText.Text = $"Прочитано маршрутов: {links.Count}. Узлов: {_networkTopology.NodeCount}. " +
                $"Компонент связности: {groups.Count}. Ссылок на трубы: {_networkTopology.PipeReferenceCount}.";
            var allWarnings = new List<string>(messages);
            foreach (var group in groups)
            {
                allWarnings.Add($"Компонента {group.Number}: {group.RouteIds.Count} маршрутов, " +
                    $"концов: {group.DegreeOneCount}, ветвлений: {group.BranchNodeCount}" +
                    (group.HasCycle ? ", ОБНАРУЖЕН ЦИКЛ." : "."));
            }
            NetworkProblemsText.Text = allWarnings.Count == 0 ?
                "Проверка прочитанных маршрутов завершена. Расходы и гидравлика не определены." :
                string.Join(Environment.NewLine, allWarnings.Take(20));
            StatusText.Text = "Диагностика связей маршрутов завершена. Не является гидравлическим расчётом.";
            Log($"Диагностика Renga: {links.Count} маршрутов, {groups.Count} компонент, {messages.Count} предупреждений.");
        }
        catch (Exception ex)
        {
            NetworkSummaryText.Text = "Ошибка чтения топологии: " + ex.Message;
            Log("Ошибка топологии: " + ex.Message);
            MessageBox.Show(ex.Message, "FireCalc — диагностика сети", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void NetworkExport_Click(object sender, RoutedEventArgs e)
    {
        if (_networkTopology is null || _networkTopology.RouteCount == 0)
        {
            MessageBox.Show("Сначала проверьте маршруты Renga.");
            return;
        }
        var save = new SaveFileDialog { FileName = "FireCalc_Renga_Связность.csv",
            Filter = "CSV UTF-8 (*.csv)|*.csv", DefaultExt = ".csv" };
        if (save.ShowDialog(this) != true) return;
        try
        {
            var rows = new List<string> {
                "RouteId;SourceId;TargetId;SystemStyleId;PipeCount;PipeIds;TreeFlowLs"
            };
            foreach (var r in _networkTopology.Routes)
            {
                string q = _treeFlows is not null && _treeFlows.RouteFlowLs.TryGetValue(r.RouteId, out var flow)
                    ? flow.ToString("0.########", CultureInfo.InvariantCulture) : "";
                rows.Add($"{r.RouteId};{r.SourceObjectId};{r.TargetObjectId};{r.SystemStyleId};" +
                    $"{r.PipeObjectIds.Count};{string.Join(",",r.PipeObjectIds)};{q}");
            }
            File.WriteAllLines(save.FileName, rows, new UTF8Encoding(true));
            Log("Экспорт связности CSV: " + save.FileName);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "FireCalc — CSV", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void NetworkFlow_Click(object sender, RoutedEventArgs e)
    {
        _treeFlows = null;
        try
        {
            if (_networkTopology is null || _networkTopology.RouteCount == 0)
                throw new InvalidOperationException("Сначала проверьте маршруты Renga.");
            var (fresh, warnings) = _bridge.ReadRouteNetwork();
            if (warnings.Any(m => m.Contains("Прочитано")) ||
                NetworkSignature(fresh) != NetworkSignature(_networkTopology.Routes))
                throw new InvalidOperationException("Сеть Renga изменилась или прочитана не полностью. Повторите диагностику.");
            if (!int.TryParse(NetworkRootBox.Text.Trim(), out var root))
                throw new ArgumentException("Укажите ID источника из таблицы.");
            var flows = new Dictionary<int,double>();
            foreach (var part in NetworkDemandsBox.Text.Split(
                new[] {';', '\r','\n'}, StringSplitOptions.RemoveEmptyEntries))
            {
                var tokens = part.Split('=', 2);
                if (tokens.Length != 2 || !int.TryParse(tokens[0].Trim(), out int node))
                    throw new ArgumentException($"Ожидается пара ID=Q. Проблема: {part}");
                string qtext = tokens[1].Trim();
                if (!double.TryParse(qtext, NumberStyles.Float, CultureInfo.GetCultureInfo("ru-RU"), out double q) &&
                    !double.TryParse(qtext, NumberStyles.Float, CultureInfo.InvariantCulture, out q))
                    throw new ArgumentException($"Некорректный Q потребителя {node}.");
                if (!flows.TryAdd(node,q)) throw new ArgumentException($"Повторно введён потребитель {node}.");
            }
            _treeFlows = _networkTopology.CalculateTreeFlows(root, flows);
            BindRouteRows();
            NetworkFlowText.Text = $"Расход источника: {_treeFlows.TotalFlowLs:0.###} л/с. " +
                $"Расходы рассчитаны для {_treeFlows.RouteFlowLs.Count} маршрутов; " +
                $"конечных потребителей: {_treeFlows.TerminalObjectIds.Count}. " +
                "Для выбранной цепи Q будут перенесены в гидравлику; Кт не определены.";
            Log($"Расходы дерева: источник {root}, Q={_treeFlows.TotalFlowLs:0.###} л/с, ветви {_treeFlows.RouteFlowLs.Count}.");
        }
        catch (Exception ex)
        {
            NetworkFlowText.Text = "Расходы не определены: " + ex.Message;
            MessageBox.Show(ex.Message, "FireCalc — расходы сети", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void NetworkPath_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // A previously read graph must match the current BIM structure.
            if (_networkTopology is null || _networkTopology.RouteCount == 0)
                throw new InvalidOperationException("Сначала нажмите «Проверить маршруты Renga».");
            var (freshLinks, freshWarnings) = _bridge.ReadRouteNetwork();
            if (freshWarnings.Any(m=>m.Contains("Прочитано")))
                throw new InvalidOperationException("Часть маршрутов не прочитана, выбор пути запрещён.");
            if (NetworkSignature(freshLinks) != NetworkSignature(_networkTopology.Routes))
                throw new InvalidOperationException("Маршруты Renga изменились. Повторно нажмите «Проверить маршруты Renga».");
            if (!int.TryParse(NetworkStartBox.Text, out int from) ||
                !int.TryParse(NetworkEndBox.Text, out int to))
                throw new ArgumentException("Введите числовые ID начального и конечного объектов.");
            var path = _networkTopology.FindUniquePath(from, to);
            // This resets all EFER demonstration assumptions and rereads pipes.
            HydroFromModel_Click(sender, e);
            var byId = _hydroRows.Where(x => x.RengaId.HasValue)
                .ToDictionary(x => x.RengaId!.Value);
            var pathSet = path.PipeObjectIds.ToHashSet();
            if (path.PipeObjectIds.Any(id => !byId.ContainsKey(id)))
                throw new InvalidOperationException("В некоторых найденных маршрутах нет соответствующих объектов Pipe текущей модели. Нельзя сформировать трассу.");
            var ordered = path.PipeObjectIds.Select(id => byId[id]).ToList();
            foreach (var row in ordered) row.Included = true;
            if (_treeFlows is not null && _treeFlows.SourceObjectId == from)
            {
                var byRoute = _networkTopology.Routes.ToDictionary(x=>x.RouteId);
                int cursor = 0;
                foreach (int routeId in path.RouteIds)
                {
                    int length = byRoute[routeId].PipeObjectIds.Count;
                    double? derived = _treeFlows.RouteFlowLs.TryGetValue(routeId, out double q)
                        ? q : null;
                    for (int j = 0; j < length && cursor < path.PipeObjectIds.Count; j++)
                        ordered[cursor++].FlowLs = derived;
                }
            }
            ordered.AddRange(_hydroRows.Where(x => !x.RengaId.HasValue || !pathSet.Contains(x.RengaId.Value)));
            SetHydroRows(ordered);
            HydroConfirmationBox.IsChecked = false;
            HydroGuidanceText.Text = "Путь предложен по связям IRouteParams. ПРОВЕРЬТЕ выбор участков, направление, расходы Q и Кт каждого материала; соединения и потери арматуры не рассчитаны.";
            NetworkPathText.Text = $"Кандидат: {path.RouteIds.Count} маршрутов, {path.PipeObjectIds.Count} труб. " +
                (_treeFlows is not null && _treeFlows.SourceObjectId == from
                  ? "Q подставлены по дереву; Кт и все данные проверяет инженер."
                  : "Q и Кт необходимо заполнить вручную.");
            MainTabs.SelectedIndex = 1;
            StatusText.Text = "Найдена единственная цепь маршрутов. Расчёт без инженерного ввода Q/Кт запрещён.";
            Log($"Кандидат трассы: объекты {from} → {to}, {path.RouteIds.Count} маршрутов, {path.PipeObjectIds.Count} труб.");
        }
        catch (Exception ex)
        {
            NetworkPathText.Text = "Путь не построен: " + ex.Message;
            MessageBox.Show(ex.Message, "FireCalc — выбор пути", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
