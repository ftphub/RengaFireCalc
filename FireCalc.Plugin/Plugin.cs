using Renga;

namespace FireCalc.Plugin;

// Pattern taken from the official 9.3 SDK / Net8PluginWithWPF sample.
public sealed class Plugin : Renga.IPlugin
{
    private Renga.IApplication? _app;
    private readonly List<Renga.ActionEventSource> _eventSources = new();
    private MainWindow? _window;

    public bool Initialize(string pluginFolder)
    {
        _app = new Renga.Application();
        var ui = _app.UI;
        var action = ui.CreateAction();
        action.DisplayName = "FireCalc · ЭФЭР";
        action.ToolTip = "Локальные расчёты ЭФЭР, гидравлика выбранной трассы, диагностика BIM";
        var eventSource = new Renga.ActionEventSource(action);
        eventSource.Triggered += OnTriggered;
        _eventSources.Add(eventSource);
        var panel = ui.CreateUIPanelExtension();
        panel.AddToolButton(action);
        ui.AddExtensionToPrimaryPanel(panel);
        return true;
    }

    private void OnTriggered(object? sender, EventArgs args)
    {
        if (_app is null) return;
        if (_window is { IsVisible: true }) { _window.Activate(); return; }
        _window = new MainWindow(new ModelBridge(_app));
        _window.Closed += (_, _) => _window = null;
        _window.Show();
    }

    public void Stop()
    {
        _window?.Close();
        foreach (var source in _eventSources) source.Dispose();
        _eventSources.Clear();
        _app = null;
    }
}
