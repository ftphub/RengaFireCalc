using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using FireCalc.Core;

namespace FireCalc.Plugin;

/// <summary>
/// Only copies/transfers actual IDs read from Renga objects; no geometry inference.
/// This UI helper never changes the model or validates hydraulic direction.
/// </summary>
public partial class MainWindow
{
    private void CopyObjectId(int? id, string description)
    {
        if (!id.HasValue || id.Value <= 0)
        {
            MessageBox.Show(this, $"Нет корректного ID: {description}. Выберите строку объекта из модели.",
                "FireCalc — копирование ID", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        try
        {
            Clipboard.SetText(id.Value.ToString(CultureInfo.InvariantCulture));
            StatusText.Text = $"ID скопирован: {id.Value} ({description}).";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Не удалось скопировать ID в буфер обмена: " + ex.Message,
                "FireCalc — буфер обмена", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private RouteDisplay? SelectedRoute()
    {
        if (NetworkGrid.SelectedItem is RouteDisplay route) return route;
        MessageBox.Show(this, "Сначала щёлкните по нужному маршруту в таблице «Маршруты и их точки подключения».",
            "FireCalc — выбор маршрута", MessageBoxButton.OK, MessageBoxImage.Information);
        return null;
    }

    private ModelBridge.PortConnectionInfo? SelectedPort()
    {
        if (PortGrid.SelectedItem is ModelBridge.PortConnectionInfo port) return port;
        MessageBox.Show(this, "Сначала выделите строку в таблице «Порты оборудования и фитингов».",
            "FireCalc — выбор порта", MessageBoxButton.OK, MessageBoxImage.Information);
        return null;
    }

    private void CopyRouteId_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRoute() is { } route) CopyObjectId(route.RouteId, "маршрут Renga");
    }
    private void CopyRouteStartId_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRoute() is { } route) CopyObjectId(route.SourceObjectId, "объект «От»");
    }
    private void CopyRouteEndId_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRoute() is { } route) CopyObjectId(route.TargetObjectId, "объект «К»");
    }

    private void RouteToStart_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRoute() is not { } route) return;
        NetworkStartBox.Text = route.SourceObjectId.ToString(CultureInfo.InvariantCulture);
        StatusText.Text = $"Начало поиска пути: объект {route.SourceObjectId} (из выбранного маршрута).";
    }
    private void RouteToEnd_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRoute() is not { } route) return;
        NetworkEndBox.Text = route.TargetObjectId.ToString(CultureInfo.InvariantCulture);
        StatusText.Text = $"Конец поиска пути: объект {route.TargetObjectId} (из выбранного маршрута).";
    }
    private void RouteToRoot_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedRoute() is not { } route) return;
        NetworkRootBox.Text = route.SourceObjectId.ToString(CultureInfo.InvariantCulture);
        StatusText.Text = $"Кандидат источника: объект {route.SourceObjectId}. Его фактическую функцию и направление потока подтвердите вручную.";
    }

    private void CopyPortObjectId_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPort() is { } port) CopyObjectId(port.ObjectId, "объект «" + port.ObjectName + "»");
    }
    private void CopyPortRouteId_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPort() is { } port) CopyObjectId(port.RouteId, "маршрут порта");
    }
    private void PortToStart_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPort() is not { } port) return;
        NetworkStartBox.Text = port.ObjectId.ToString(CultureInfo.InvariantCulture);
        StatusText.Text = $"Начало пути: объект {port.ObjectId}. Направление потока не установлено.";
    }
    private void PortToEnd_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPort() is not { } port) return;
        NetworkEndBox.Text = port.ObjectId.ToString(CultureInfo.InvariantCulture);
        StatusText.Text = $"Конец пути: объект {port.ObjectId}.";
    }
    private void PortToRoot_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPort() is not { } port) return;
        NetworkRootBox.Text = port.ObjectId.ToString(CultureInfo.InvariantCulture);
        StatusText.Text = $"Кандидат источника: объект {port.ObjectId}. Требуется инженерное подтверждение.";
    }
    private void AddPumpIdFromPort_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedPort() is not { } port) return;
        if (port.ObjectId <= 0 || !port.ObjectName.Contains("насос", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this,
                "Выбранная строка не обозначена как насос. Выберите порт объекта «Насос»; наименование не заменяет проверку типа оборудования.",
                "FireCalc — ID насоса", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var ids = new List<int>();
        foreach (var token in PumpIdBox.Text.Split(new[] {',', ';', ' ', '\r', '\n'}, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id <= 0)
            {
                MessageBox.Show(this, "Поле ID насосов содержит некорректные значения. Исправьте их перед добавлением.",
                    "FireCalc — ID насоса", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!ids.Contains(id)) ids.Add(id);
        }
        if (!ids.Contains(port.ObjectId)) ids.Add(port.ObjectId);
        PumpIdBox.Text = string.Join(";", ids);
        PumpScenarioText.Text = "ID насосов изменены. Сценарий ещё не сохранён; проверьте назначения оборудования и нажмите «Сценарий насосов».";
        StatusText.Text = $"Добавлен ID {port.ObjectId} в список насосов. Режим работы не определён.";
    }

    private void CopyPipeId_Click(object sender, RoutedEventArgs e)
    {
        if (PipesGrid.SelectedItem is PipeSegment pipe)
            CopyObjectId(pipe.RengaObjectId, "труба Renga");
        else
            MessageBox.Show(this, "Выделите строку трубы в таблице «Трубы / Renga».",
                "FireCalc — выбор трубы", MessageBoxButton.OK, MessageBoxImage.Information);
    }
    private void CopyHydroPipeId_Click(object sender, RoutedEventArgs e)
    {
        if (HydroGrid.SelectedItem is HydraulicUiRow row)
            CopyObjectId(row.RengaId, "участок «" + row.Name + "»");
        else
            MessageBox.Show(this, "Выделите строку в таблице гидравлических участков.",
                "FireCalc — выбор участка", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
