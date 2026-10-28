using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using FireCalc.Core;

namespace FireCalc.Plugin;

public partial class MainWindow
{
    private List<ModelBridge.PortConnectionInfo> _portConnections = new();

    private void PortScan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (ports, warnings) = _bridge.ReadPortConnections();
            _portConnections = ports;
            PortGrid.ItemsSource = null;
            PortGrid.ItemsSource = ports.Where(p => p.RouteId > 0).ToList();
            var routeCount = ports.Select(x=>x.RouteId).Where(id=>id>0).Distinct().Count();
            PortSummaryText.Text = $"Портов с подключением к маршрутам: {ports.Count(p => p.RouteId > 0)}, " +
                $"маршрутов: {routeCount}, объектов: {ports.Select(p=>p.ObjectId).Distinct().Count()}.";
            var objectsWithMultipleRoutes = ports.Where(p=>p.RouteId>0).GroupBy(p=>p.ObjectId)
                .Where(g=>g.Select(p=>p.RouteId).Distinct().Count()>1).ToList();
            PortDetailText.Text = objectsWithMultipleRoutes.Count == 0
                ? "Объектов, у которых порты связаны с несколькими маршрутами, не обнаружено. Это НЕ доказывает, что маршруты физически независимы."
                : "Объекты с портами на разных маршрутах: " + string.Join("; ",objectsWithMultipleRoutes
                    .Select(g => $"{g.Key} [{string.Join(",",g.Select(p=>p.RouteId).Distinct())}]")).Trim() +
                  ". Возможность протока через оборудование и фитинги требует проверки.";
            if (warnings.Count > 0) PortDetailText.Text += "\nПредупреждения: " + string.Join("; ", warnings.Take(5));
            StatusText.Text = "Диагностика инженерных портов завершена. Направления течения и режим насосов не определены.";
            Log($"Порты: {ports.Count} прочитано; {objectsWithMultipleRoutes.Count} кандидатов для анализа смежных маршрутов.");
        }
        catch (Exception ex)
        {
            PortSummaryText.Text = "Ошибка чтения портов: " + ex.Message;
            Log("Ошибка чтения портов: " + ex.Message);
            MessageBox.Show(ex.Message, "FireCalc — инженерные порты", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PumpSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var ids = PumpIdBox.Text.Split(new[]{',',';', ' ', '\r','\n'}, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => int.TryParse(t.Trim(), out var id) ? id : -1).ToArray();
            var mode = (PumpMode)PumpModeBox.SelectedIndex;
            var description = PumpScenario.Describe(mode, ids);
            PumpScenarioText.Text = description;
            Log($"Сценарий насосов: {mode}. ID: {string.Join(",", ids)}. {description}");
            StatusText.Text = "Сценарий насосов задан описательно. Подбор и гидравлический расчёт насосов НЕ производились.";
        }
        catch (Exception ex)
        {
            PumpScenarioText.Text = "Не удалось задать сценарий: " + ex.Message;
            MessageBox.Show(ex.Message, "FireCalc — насосы", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
