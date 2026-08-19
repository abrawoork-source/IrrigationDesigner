// File: src/IrrigationCalc/Calculations/HydraulicsEngine.cs
using IrrigationCalc.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IrrigationCalc.Calculations;

/// <summary>
/// Hazen-Williams pipe network hydraulics for a TREE (no-loop) network.
/// Traversal is depth-first from source; loops cause an immediate error return.
/// </summary>
public class HydraulicsEngine
{
    private readonly ILogger<HydraulicsEngine> _logger;

    public HydraulicsEngine(ILogger<HydraulicsEngine>? logger = null)
    {
        _logger = logger ?? NullLogger<HydraulicsEngine>.Instance;
    }

    // ── Default Hazen-Williams C factors ─────────────────────────────────────
    private static double DefaultC(PipeMaterial material) =>
        material switch
        {
            PipeMaterial.PVC => 140,
            PipeMaterial.PE  => 130,
            _                => 130
        };

    // ── Hazen-Williams head-loss (SI) ─────────────────────────────────────────
    // hf = 10.67 * L * Q^1.852 / (C^1.852 * d^4.871)
    // Q in m³/s, d in m, L in m → hf in m
    public static double HazenWilliamsHeadLoss(
        double length_m, double flow_m3s, double C, double diameter_m)
    {
        if (diameter_m <= 0 || flow_m3s <= 0) return 0;
        return 10.67 * length_m * Math.Pow(flow_m3s, 1.852)
               / (Math.Pow(C, 1.852) * Math.Pow(diameter_m, 4.871));
    }

    // ── Velocity ──────────────────────────────────────────────────────────────
    public static double Velocity_ms(double flow_m3s, double diameter_m)
    {
        if (diameter_m <= 0) return 0;
        double area = Math.PI * diameter_m * diameter_m / 4.0;
        return area <= 0 ? 0 : flow_m3s / area;
    }

    // ── Main solver ───────────────────────────────────────────────────────────
    public HydraulicResult Solve(HydraulicInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var result = new HydraulicResult();

        // Deep-copy nodes so caller's objects are not mutated
        var nodes    = input.Nodes.Select(n => Clone(n)).ToList();
        var segments = input.Segments.Select(s => Clone(s)).ToList();
        result.Nodes    = nodes;
        result.Segments = segments;

        if (!nodes.Any())
        {
            result.Success      = false;
            result.ErrorMessage = "No nodes defined.";
            return result;
        }

        // ── Build adjacency for tree check ────────────────────────────────────
        var nodeMap = nodes.ToDictionary(n => n.NodeId);
        // adjacency list: fromNode -> list of (toNode, segmentId)
        var adj = new Dictionary<int, List<(int to, int segId)>>();
        foreach (var n in nodes) adj[n.NodeId] = new();
        foreach (var seg in segments)
        {
            if (!nodeMap.ContainsKey(seg.FromNodeId) || !nodeMap.ContainsKey(seg.ToNodeId))
            {
                result.Success      = false;
                result.ErrorMessage = $"Segment {seg.SegmentId} references unknown node.";
                return result;
            }
            adj[seg.FromNodeId].Add((seg.ToNodeId, seg.SegmentId));
        }

        // ── Loop detection via DFS ────────────────────────────────────────────
        var visited = new HashSet<int>();
        var inStack = new HashSet<int>();
        bool hasLoop = false;
        DfsLoopCheck(input.SourceNodeId, -1, adj, visited, inStack, ref hasLoop);
        if (hasLoop)
        {
            result.Success      = false;
            result.ErrorMessage = "Loop detected in pipe network. Version 1 supports tree networks only. Please remove loops.";
            return result;
        }

        // ── Assign C values from material ─────────────────────────────────────
        foreach (var seg in segments)
            if (seg.HazenWilliamsC <= 0)
                seg.HazenWilliamsC = DefaultC(seg.Material);

        // ── DFS: accumulate downstream demand ────────────────────────────────
        AccumulateDemand(input.SourceNodeId, -1, adj, segments.ToDictionary(s => s.SegmentId), nodeMap);

        // ── DFS: compute pressures from source ────────────────────────────────
        var sourceNode = nodeMap[input.SourceNodeId];
        sourceNode.ComputedPressure_bar = input.SourcePressure_bar
                                         + UnitConverter.MetersToBar(
                                             input.SourceElevation_m - sourceNode.Elevation_m);

        var segMap = segments.ToDictionary(s => s.SegmentId);
        PropagatePressure(input.SourceNodeId, -1, adj, segMap, nodeMap);

        // ── Calculate velocity and pressure drop per segment ──────────────────
        foreach (var seg in segments)
        {
            double totalLen = seg.Length_m + seg.FittingsEquivLength_m;
            double diam_m   = UnitConverter.MmToM(seg.Diameter_mm);
            double flow_m3s = UnitConverter.LminToM3s(seg.Flow_Lmin);

            seg.Velocity_ms      = Velocity_ms(flow_m3s, diam_m);
            seg.HeadLoss_m       = HazenWilliamsHeadLoss(totalLen, flow_m3s, seg.HazenWilliamsC, diam_m);
            seg.PressureDrop_bar = UnitConverter.MetersToBar(seg.HeadLoss_m);
        }

        // ── Check minimum required pressure at head nodes ─────────────────────
        foreach (var kv in input.HeadNodeRequiredPressure)
        {
            if (!nodeMap.TryGetValue(kv.Key, out var node)) continue;
            if (node.ComputedPressure_bar < kv.Value - 0.001)
                result.Warnings.Add(
                    $"Node {kv.Key}: computed pressure {node.ComputedPressure_bar:F2} bar is below required {kv.Value:F2} bar.");
        }

        result.Success = true;
        _logger.LogInformation("Hydraulic solve complete. {SegCount} segments, {WarnCount} warnings.",
            segments.Count, result.Warnings.Count);
        return result;
    }

    // ── DFS loop detection ────────────────────────────────────────────────────
    private static void DfsLoopCheck(
        int node, int parent,
        Dictionary<int, List<(int to, int segId)>> adj,
        HashSet<int> visited, HashSet<int> inStack, ref bool hasLoop)
    {
        visited.Add(node);
        inStack.Add(node);
        foreach (var (to, _) in adj[node])
        {
            if (to == parent) continue; // undirected tree edge
            if (inStack.Contains(to)) { hasLoop = true; return; }
            if (!visited.Contains(to))
                DfsLoopCheck(to, node, adj, visited, inStack, ref hasLoop);
            if (hasLoop) return;
        }
        inStack.Remove(node);
    }

    // ── DFS demand accumulation (post-order) ──────────────────────────────────
    private static void AccumulateDemand(
        int nodeId, int parentSegId,
        Dictionary<int, List<(int to, int segId)>> adj,
        Dictionary<int, HydraulicSegment> segMap,
        Dictionary<int, HydraulicNode> nodeMap)
    {
        double childDemand = 0;
        foreach (var (childId, segId) in adj[nodeId])
        {
            AccumulateDemand(childId, segId, adj, segMap, nodeMap);
            childDemand += nodeMap[childId].DemandFlow_Lmin;
        }
        // Node demand = its own head demand + sum of children demands
        nodeMap[nodeId].DemandFlow_Lmin += childDemand;

        // Set segment flow = child node's total demand
        if (parentSegId >= 0 && segMap.TryGetValue(parentSegId, out var seg))
            seg.Flow_Lmin = nodeMap[nodeId].DemandFlow_Lmin;
    }

    // ── DFS pressure propagation (pre-order) ─────────────────────────────────
    private static void PropagatePressure(
        int nodeId, int parentId,
        Dictionary<int, List<(int to, int segId)>> adj,
        Dictionary<int, HydraulicSegment> segMap,
        Dictionary<int, HydraulicNode> nodeMap)
    {
        var node = nodeMap[nodeId];
        foreach (var (childId, segId) in adj[nodeId])
        {
            var seg       = segMap[segId];
            var childNode = nodeMap[childId];

            double totalLen  = seg.Length_m + seg.FittingsEquivLength_m;
            double diam_m    = UnitConverter.MmToM(seg.Diameter_mm);
            double flow_m3s  = UnitConverter.LminToM3s(seg.Flow_Lmin);
            double hf        = HazenWilliamsHeadLoss(totalLen, flow_m3s, seg.HazenWilliamsC, diam_m);
            double elDiff    = node.Elevation_m - childNode.Elevation_m; // positive if child is lower
            double elevGain  = UnitConverter.MetersToBar(elDiff);        // pressure increases going down

            childNode.ComputedPressure_bar =
                node.ComputedPressure_bar
                - UnitConverter.MetersToBar(hf)
                + elevGain;

            PropagatePressure(childId, nodeId, adj, segMap, nodeMap);
        }
    }

    // ── Clone helpers ─────────────────────────────────────────────────────────
    private static HydraulicNode Clone(HydraulicNode n) => new()
    {
        NodeId               = n.NodeId,
        Elevation_m          = n.Elevation_m,
        Type                 = n.Type,
        DemandFlow_Lmin      = n.DemandFlow_Lmin,
        ComputedPressure_bar = n.ComputedPressure_bar,
        ChildNodeIds         = new List<int>(n.ChildNodeIds)
    };

    private static HydraulicSegment Clone(HydraulicSegment s) => new()
    {
        SegmentId             = s.SegmentId,
        FromNodeId            = s.FromNodeId,
        ToNodeId              = s.ToNodeId,
        Material              = s.Material,
        Diameter_mm           = s.Diameter_mm,
        Length_m              = s.Length_m,
        FittingsEquivLength_m = s.FittingsEquivLength_m,
        HazenWilliamsC        = s.HazenWilliamsC
    };
}
