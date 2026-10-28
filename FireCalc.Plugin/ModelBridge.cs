using FireCalc.Core;
using Renga;

namespace FireCalc.Plugin;

/// <summary>
/// Renga-only boundary. Calculations remain testable without COM or Renga.
/// The pipeline deliberately does NOT identify hydraulic circuits / calculate Wтр from ALL pipes.
/// </summary>
public sealed class ModelBridge(Renga.IApplication app)
{
    private static readonly Guid PipeVolumePropertyId = new("7A8B0289-93BF-4A32-A4FE-91DA257F64F2");

    public List<PipeSegment> ReadPipes()
    {
        var result = new List<PipeSegment>();
        var objects = app.Project.Model.GetObjects();
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects.GetByIndex(i);
            if (obj.ObjectType != Renga.EntityTypes.Pipe) continue;
            double? length = null;
            double? diameter = null;
            try
            {
                var q = obj.GetQuantities().Get(Renga.Quantities.NominalLength);
                if (q is not null)
                {
                    double value = q.AsLength(Renga.LengthUnit.LengthUnit_Meters);
                    if (double.IsFinite(value) && value > 0) length = value;
                }
            }
            catch { /* no quantity; show as incomplete instead of inventing length */ }
            string diameterSource = "Не удалось определить";
            // IMPORTANT (Renga SDK API 2.50): PipeInsideDiameter belongs to PipeStyle.
            // Pipe instances expose PipeStyleId. Do not infer inner diameter from DN,
            // outer swept geometry or the displayed name (e.g. PPR 20x3.4).
            try
            {
                var instanceParameters = obj.GetParameters();
                diameter = TryReadInnerDiameterMm(instanceParameters);
                if (diameter.HasValue)
                    diameterSource = "Параметр объекта";
                else
                {
                    var styleIdParameter = instanceParameters?.Get(Renga.Parameters.PipeStyleId);
                    if (styleIdParameter is null || !styleIdParameter.HasValue)
                        diameterSource = "Нет PipeStyleId";
                    else if (styleIdParameter.ValueType != Renga.ParameterValueType.ParameterValueType_Int)
                        diameterSource = "PipeStyleId: неверный тип";
                    else
                    {
                        int styleId = styleIdParameter.GetIntValue();
                        var pipeStyle = app.Project.PipeStyles.GetById(styleId);
                        if (pipeStyle is Renga.IParameterContainer styleParameters)
                        {
                            diameter = TryReadInnerDiameterMm(styleParameters);
                            diameterSource = diameter.HasValue
                                ? "Стиль трубы" : "Нет Dвн в стиле трубы";
                        }
                        else
                            diameterSource = "Не найден стиль трубы";
                    }
                }
            }
            catch (Exception ex)
            {
                // Diagnostics remain visible even if COM returns a missing interface.
                diameterSource = "Renga API: " + ex.GetType().Name;
            }
            result.Add(new PipeSegment(obj.Id, obj.Name ?? $"Труба {obj.Id}", length, diameter, diameterSource));
        }
        return result;
    }

    // All built-in Renga pipe style length parameters are in millimetres (SDK v2.50).
    private static double? TryReadInnerDiameterMm(Renga.IParameterContainer? parameters)
    {
        if (parameters is null) return null;
        var parameter = parameters.Get(Renga.Parameters.PipeInsideDiameter);
        if (parameter is null || !parameter.HasValue) return null;
        double raw = parameter.ValueType switch
        {
            Renga.ParameterValueType.ParameterValueType_Double => parameter.GetDoubleValue(),
            Renga.ParameterValueType.ParameterValueType_Int => parameter.GetIntValue(),
            _ => double.NaN
        };
        // Safety gate: impossible dimensions are never silently converted or used.
        return double.IsFinite(raw) && raw >= 1 && raw <= 3000 ? raw : null;
    }

    /// <summary>
    /// Inspect Renga Route objects without changing a project.
    /// Connectivity is taken from IRouteParams and is NOT inferred from overlapping geometry.
    /// Some route types / connections might not expose IRouteParams; all are logged, never guessed.
    /// </summary>
    public (List<RouteLink> Links, List<string> Messages) ReadRouteNetwork()
    {
        var links = new List<RouteLink>();
        var messages = new List<string>();
        var objects = app.Project.Model.GetObjects();
        int candidates = 0;
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects.GetByIndex(i);
            if (obj.ObjectType != Renga.EntityTypes.Route) continue;
            candidates++;
            try
            {
                var parameters = obj.GetInterfaceByName("IRouteParams") as Renga.IRouteParams;
                if (parameters is null)
                {
                    messages.Add($"Маршрут {obj.Id}: интерфейс IRouteParams недоступен.");
                    continue;
                }
                int source = parameters.SourceModelObjectId;
                int target = parameters.TargetModelObjectId;
                if (source <= 0 || target <= 0 || source == target)
                {
                    messages.Add($"Маршрут {obj.Id}: неопределённые/совпадающие концы {source} и {target}.");
                    continue;
                }
                var pipes = new List<int>();
                int count = parameters.GetFlowSegmentCount();
                for (int k = 0; k < count; k++)
                {
                    int id = parameters.GetFlowSegmentPlacement(k).Id;
                    var flowObject = objects.GetById(id);
                    if (flowObject is not null && flowObject.ObjectType == Renga.EntityTypes.Pipe)
                        pipes.Add(id);
                }
                links.Add(new RouteLink(obj.Id, source, target,
                    pipes.Distinct().ToArray(), parameters.SystemStyleId));
                if (pipes.Count == 0)
                    messages.Add($"Маршрут {obj.Id}: труб на маршруте не обнаружено (возможна другая инженерная категория).");
            }
            catch (Exception ex)
            {
                messages.Add($"Маршрут {obj.Id}: ошибка чтения {ex.GetType().Name}: {ex.Message}");
            }
        }
        if (candidates == 0)
            messages.Add("Объекты типа Route не обнаружены. Возможна несвязанная геометрия труб или другой способ создания трасс.");
        if (links.Count < candidates)
            messages.Add($"Прочитано {links.Count} из {candidates} объектов Route; анализ может быть неполным.");
        return (links, messages);
    }

    public sealed record PortConnectionInfo(int ObjectId, string ObjectName, int PortIndex, string PortName, int RouteId, string FlowDirection);

    /// <summary>
    /// Read-only port inspection from the Renga 9.3 SDK. The two ports of a pump do NOT
    /// establish a validated hydraulic pass-through, and route direction is not a flow direction.
    /// </summary>
    public (List<PortConnectionInfo> Ports, List<string> Warnings) ReadPortConnections()
    {
        var result = new List<PortConnectionInfo>();
        var warnings = new List<string>();
        var objects = app.Project.Model.GetObjects();
        for (int i = 0; i < objects.Count; i++)
        {
            var obj = objects.GetByIndex(i);
            if (obj.ObjectType == Renga.EntityTypes.Route) continue;
            try
            {
                var ports = obj.GetInterfaceByName("IEntityWithPorts") as Renga.IEntityWithPorts;
                if (ports is null) continue;
                for (int p = 0; p < ports.Count; p++)
                {
                    try
                    {
                        var port = ports.GetByIndex(p);
                        if (port is null || !port.HasConnectionWithRoute()) continue;
                        result.Add(new PortConnectionInfo(obj.Id, obj.Name ?? $"Объект {obj.Id}", p,
                            port.Name ?? $"Порт {p}", port.ConnectedRouteId(), port.FlowDirection.ToString()));
                    }
                    catch (Exception ex)
                    {
                        warnings.Add($"Объект {obj.Id}, порт {p}: {ex.GetType().Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                // Some entity types do not implement IEntityWithPorts. If obtaining it throws,
                // keep diagnostics rather than inferring a made-up connection.
                if (warnings.Count < 30)
                    warnings.Add($"Объект {obj.Id}: {ex.GetType().Name}");
            }
        }
        return (result, warnings);
    }

    public int WritePipeVolumes(IReadOnlyList<PipeSegment> segments)
    {
        var complete = segments.Where(p => p.InternalVolumeM3.HasValue).ToList();
        if (complete.Count == 0) throw new InvalidOperationException("Нет труб с заполненными длиной и внутренним диаметром.");
        var project = app.Project;
        if (project.HasActiveOperation()) throw new InvalidOperationException("В проекте уже выполняется операция редактирования.");
        var operation = project.CreateOperation();
        operation.Start();
        try
        {
            var pm = project.PropertyManager;
            if (!pm.IsPropertyRegistered(PipeVolumePropertyId))
                pm.RegisterProperty(PipeVolumePropertyId, new Renga.PropertyDescription {
                    Name = "FireCalc: объём участка трубы, м³",
                    Type = Renga.PropertyType.PropertyType_Double });
            if (!pm.IsPropertyAssignedToType(PipeVolumePropertyId, Renga.EntityTypes.Pipe))
                pm.AssignPropertyToType(PipeVolumePropertyId, Renga.EntityTypes.Pipe);
            int count = 0;
            foreach (var segment in complete)
            {
                var obj = project.Model.GetObjects().GetById(segment.RengaObjectId);
                if (obj is null || obj.ObjectType != Renga.EntityTypes.Pipe) continue;
                var prop = obj.GetProperties().Get(PipeVolumePropertyId);
                if (prop is null) continue;
                prop.SetDoubleValue(segment.InternalVolumeM3!.Value);
                count++;
            }
            operation.Apply();
            return count;
        }
        catch { operation.Rollback(); throw; }
    }
}
