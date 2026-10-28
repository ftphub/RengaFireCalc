using System;
using System.Collections.Generic;
using System.Linq;

namespace FireCalc.Core;

/// <summary>
/// Read-only connectivity at the Renga ROUTE level. This is not a hydraulic solver:
/// routes between model objects can have branches, fittings, or no accessible pipe segments.
/// Never infer flow direction or water demand from graph geometry.
/// </summary>
public sealed record RouteLink(int RouteId, int SourceObjectId, int TargetObjectId,
    IReadOnlyList<int> PipeObjectIds, int SystemStyleId = 0);
public sealed record NetworkComponent(int Number, IReadOnlyList<int> NodeIds,
    IReadOnlyList<int> RouteIds, int DegreeOneCount, int BranchNodeCount, bool HasCycle);
public sealed record NetworkPath(IReadOnlyList<int> NodeIds,
    IReadOnlyList<int> RouteIds, IReadOnlyList<int> PipeObjectIds);
public sealed record TreeFlowResult(int SourceObjectId, double TotalFlowLs,
    IReadOnlyDictionary<int, double> RouteFlowLs, IReadOnlyList<int> TerminalObjectIds);

public sealed class NetworkTopology
{
    private readonly List<RouteLink> _edges;
    private readonly Dictionary<int, List<RouteLink>> _adj = new();

    public NetworkTopology(IEnumerable<RouteLink> edges)
    {
        _edges = edges?.ToList() ?? throw new ArgumentNullException(nameof(edges));
        if (_edges.Select(x => x.RouteId).Distinct().Count() != _edges.Count)
            throw new ArgumentException("Маршруты имеют повторяющиеся ID.");
        foreach (var e in _edges)
        {
            if (e.RouteId <= 0 || e.SourceObjectId <= 0 || e.TargetObjectId <= 0)
                throw new ArgumentException("Маршрут содержит некорректный ID.");
            if (e.SourceObjectId == e.TargetObjectId)
                throw new ArgumentException($"Маршрут {e.RouteId} ссылается на одинаковые концы.");
            if (!_adj.ContainsKey(e.SourceObjectId)) _adj.Add(e.SourceObjectId, new());
            if (!_adj.ContainsKey(e.TargetObjectId)) _adj.Add(e.TargetObjectId, new());
            _adj[e.SourceObjectId].Add(e);
            _adj[e.TargetObjectId].Add(e);
        }
    }

    public int RouteCount => _edges.Count;
    public int NodeCount => _adj.Count;
    public int PipeReferenceCount => _edges.Sum(x => x.PipeObjectIds.Count);
    public IReadOnlyList<RouteLink> Routes => _edges;

    public IReadOnlyList<NetworkComponent> Components()
    {
        var visited = new HashSet<int>();
        var groups = new List<NetworkComponent>();
        foreach (var root in _adj.Keys.OrderBy(x=>x))
        {
            if (!visited.Add(root)) continue;
            var queue = new Queue<int>();
            queue.Enqueue(root);
            var nodes = new List<int>();
            var routeIds = new HashSet<int>();
            while (queue.Count != 0)
            {
                int node = queue.Dequeue();
                nodes.Add(node);
                foreach (var edge in _adj[node])
                {
                    routeIds.Add(edge.RouteId);
                    var other = Other(edge, node);
                    if (visited.Add(other)) queue.Enqueue(other);
                }
            }
            nodes.Sort();
            int endpoints = nodes.Count(n => _adj[n].Count == 1);
            int branches = nodes.Count(n => _adj[n].Count > 2);
            // For a connected undirected multigraph, E >= V means at least one cycle.
            bool cyclic = routeIds.Count >= nodes.Count;
            groups.Add(new NetworkComponent(groups.Count + 1, nodes,
                routeIds.OrderBy(x=>x).ToArray(), endpoints, branches, cyclic));
        }
        return groups;
    }

    /// <summary>
    /// Only one *simple route-level path* may exist. Rings/multiple alternatives are rejected,
    /// never silently choose the path with a shortest length or fewest pipe objects.
    /// </summary>
    public NetworkPath FindUniquePath(int startObjectId, int endObjectId)
    {
        if (startObjectId == endObjectId) throw new ArgumentException("Начало и конец должны отличаться.");
        if (!_adj.ContainsKey(startObjectId) || !_adj.ContainsKey(endObjectId))
            throw new ArgumentException("Указанный объект не найден среди концов маршрутов Renga.");
        var visited = new HashSet<int> {startObjectId};
        var currentNodes = new List<int>{startObjectId};
        var currentEdges = new List<RouteLink>();
        var solutions = new List<(int[] nodes, RouteLink[] edges)>();
        void Search(int at)
        {
            if (solutions.Count >= 2) return;
            if (at == endObjectId)
            {
                solutions.Add((currentNodes.ToArray(), currentEdges.ToArray()));
                return;
            }
            foreach (var edge in _adj[at].OrderBy(x=>x.RouteId))
            {
                int next = Other(edge, at);
                if (!visited.Add(next)) continue;
                currentNodes.Add(next);
                currentEdges.Add(edge);
                Search(next);
                currentEdges.RemoveAt(currentEdges.Count - 1);
                currentNodes.RemoveAt(currentNodes.Count - 1);
                visited.Remove(next);
                if (solutions.Count >= 2) return;
            }
        }
        Search(startObjectId);
        if (solutions.Count == 0) throw new InvalidOperationException("Между объектами нет связанного маршрута.");
        if (solutions.Count > 1) throw new InvalidOperationException("Обнаружено несколько путей между объектами (кольцо/альтернативные соединения). Требуется выбор инженера.");
        var s = solutions[0];
        if (s.edges.Select(x => x.SystemStyleId).Distinct().Count() > 1)
            throw new InvalidOperationException("На пути обнаружены разные стили инженерных систем. Объединение запрещено до проверки инженером.");
        if (s.edges.Any(e => e.PipeObjectIds.Count == 0))
            throw new InvalidOperationException("На выбранном пути есть маршруты без определённых труб. Автоматически выбирать трассу небезопасно.");
        // Orient each route's pipe sequence along the requested path when traversed backwards.
        var pipes = new List<int>();
        for (int i = 0; i < s.edges.Length; i++)
        {
            var edgePipes = s.edges[i].PipeObjectIds;
            pipes.AddRange(s.edges[i].SourceObjectId == s.nodes[i]
                ? edgePipes : edgePipes.Reverse());
        }
        if (pipes.Count != pipes.Distinct().Count())
            throw new InvalidOperationException("Одна труба встречается в нескольких маршрутах. Требуется проверка исходной модели.");
        return new NetworkPath(s.nodes, s.edges.Select(x=>x.RouteId).ToArray(), pipes.ToArray());
    }

    /// <summary>
    /// Conservation-of-flow on ONE fully described tree with ONE source and user-supplied
    /// terminal demands. Zero demand must be explicitly entered. Not valid for rings,
    /// pressure-dependent consumers, unknown bypasses, or multiple supply sources.
    /// </summary>
    public TreeFlowResult CalculateTreeFlows(int sourceId, IReadOnlyDictionary<int, double> demands)
    {
        if (!_adj.ContainsKey(sourceId)) throw new ArgumentException("Источник не найден среди концов маршрутов.");
        if (demands is null) throw new ArgumentNullException(nameof(demands));
        var nodes = new HashSet<int>();
        var q = new Queue<int>();
        var parent = new Dictionary<int,int>();
        var parentRoute = new Dictionary<int,int>();
        q.Enqueue(sourceId);
        nodes.Add(sourceId);
        var order = new List<int>();
        while (q.Count > 0)
        {
            int at = q.Dequeue(); order.Add(at);
            foreach (var edge in _adj[at])
            {
                int next = Other(edge, at);
                if (nodes.Add(next))
                {
                    parent[next] = at;
                    parentRoute[next] = edge.RouteId;
                    q.Enqueue(next);
                }
            }
        }
        int edgeCount = _edges.Count(e => nodes.Contains(e.SourceObjectId) && nodes.Contains(e.TargetObjectId));
        if (_edges.Where(e => nodes.Contains(e.SourceObjectId) && nodes.Contains(e.TargetObjectId))
            .Select(e=>e.SystemStyleId).Distinct().Count() > 1)
            throw new InvalidOperationException("В компоненте смешаны стили инженерных систем. Расчёт расхода запрещён.");
        if (edgeCount != nodes.Count - 1)
            throw new InvalidOperationException("Компонента содержит цикл или параллельные пути: расчёт дерева невозможен.");
        var leaves = nodes.Where(n => n != sourceId && _adj[n].Count == 1).OrderBy(n=>n).ToArray();
        if (leaves.Length == 0) throw new InvalidOperationException("Потребители не обнаружены.");
        foreach (var n in leaves)
            if (!demands.ContainsKey(n))
                throw new InvalidOperationException($"У конечного потребителя {n} не задан Q. Задайте даже нулевой расход явно.");
        foreach (var pair in demands)
        {
            if (!nodes.Contains(pair.Key))
                throw new ArgumentException($"Потребитель {pair.Key} не принадлежит компоненте выбранного источника.");
            if (pair.Key == sourceId)
                throw new ArgumentException("Расход задаётся потребителям, а не источнику.");
            if (!double.IsFinite(pair.Value) || pair.Value < 0)
                throw new ArgumentException($"Потребитель {pair.Key}: нужен конечный Q ≥ 0.");
        }
        var sums = nodes.ToDictionary(n => n, n => demands.TryGetValue(n,out var v) ? v : 0.0);
        var routeFlows = new Dictionary<int,double>();
        foreach (int node in order.AsEnumerable().Reverse())
        {
            if (node == sourceId) continue;
            routeFlows.Add(parentRoute[node], sums[node]);
            sums[parent[node]] += sums[node];
        }
        if (sums[sourceId] <= 0)
            throw new InvalidOperationException("Суммарный расход потребителей равен нулю.");
        return new TreeFlowResult(sourceId, sums[sourceId], routeFlows, leaves);
    }

    private static int Other(RouteLink edge, int from) => edge.SourceObjectId == from
        ? edge.TargetObjectId : edge.SourceObjectId;
}

/// <summary>Pure geometry only. High velocity threshold is a UI warning, NOT a fire safety limit.</summary>
public static class FlowDiagnostics
{
    public static double VelocityMetersPerSecond(double flowLitresPerSecond, double diameterMm)
    {
        if (!double.IsFinite(flowLitresPerSecond) || flowLitresPerSecond < 0 ||
            !double.IsFinite(diameterMm) || diameterMm <= 0)
            throw new ArgumentException("Для скорости требуются Q ≥ 0 и Dвн > 0.");
        var diameterM = diameterMm / 1000.0;
        return flowLitresPerSecond / 1000.0 / (Math.PI * diameterM * diameterM / 4.0);
    }
}
