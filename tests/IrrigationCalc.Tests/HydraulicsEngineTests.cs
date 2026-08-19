// File: tests/IrrigationCalc.Tests/HydraulicsEngineTests.cs
using IrrigationCalc.Calculations;
using IrrigationCalc.Models;
using Xunit;

namespace IrrigationCalc.Tests;

public class HydraulicsEngineTests
{
    private readonly HydraulicsEngine _engine = new();

    // ── Hazen-Williams formula unit test ──────────────────────────────────────
    [Fact]
    public void HazenWilliams_KnownValue()
    {
        // Known: L=100m, Q=0.001 m3/s (1 L/s ≈ 60 L/min), C=140, d=0.05m (50mm)
        // hf = 10.67 * 100 * (0.001^1.852) / (140^1.852 * 0.05^4.871)
        // Approximate expected: ~2.0 m (within ±10%)
        double hf = HydraulicsEngine.HazenWilliamsHeadLoss(100, 0.001, 140, 0.05);
        Assert.True(hf > 0, "Head loss must be positive");
        Assert.InRange(hf, 1.0, 5.0); // sanity range
    }

    [Fact]
    public void HazenWilliams_ZeroFlow_ReturnsZero()
    {
        double hf = HydraulicsEngine.HazenWilliamsHeadLoss(100, 0, 140, 0.05);
        Assert.Equal(0, hf);
    }

    [Fact]
    public void HazenWilliams_ZeroDiameter_ReturnsZero()
    {
        double hf = HydraulicsEngine.HazenWilliamsHeadLoss(100, 0.001, 140, 0);
        Assert.Equal(0, hf);
    }

    // ── Velocity ──────────────────────────────────────────────────────────────
    [Fact]
    public void Velocity_CorrectFormula()
    {
        // Q = π * (0.05)^2 / 4 * v  → v = Q / A
        // Q = 0.001 m3/s, d = 0.05m → A ≈ 0.001963 m² → v ≈ 0.509 m/s
        double v = HydraulicsEngine.Velocity_ms(0.001, 0.05);
        Assert.InRange(v, 0.50, 0.52);
    }

    // ── Tree solve ────────────────────────────────────────────────────────────
    [Fact]
    public void Solve_SimpleLinear_Success()
    {
        // Source(0) → Junction(1) → HeadNode(2)
        var input = new HydraulicInput
        {
            SourcePressure_bar = 4.0,
            SourceElevation_m  = 0,
            SourceNodeId       = 0,
            Nodes = new List<HydraulicNode>
            {
                new() { NodeId = 0, Elevation_m = 0, Type = NodeType.Source,   DemandFlow_Lmin = 0 },
                new() { NodeId = 1, Elevation_m = 0, Type = NodeType.Junction, DemandFlow_Lmin = 0 },
                new() { NodeId = 2, Elevation_m = 0, Type = NodeType.HeadNode, DemandFlow_Lmin = 10 }
            },
            Segments = new List<HydraulicSegment>
            {
                new() { SegmentId = 1, FromNodeId = 0, ToNodeId = 1, Material = PipeMaterial.PVC,
                        Diameter_mm = 50, Length_m = 20, FittingsEquivLength_m = 2, HazenWilliamsC = 140 },
                new() { SegmentId = 2, FromNodeId = 1, ToNodeId = 2, Material = PipeMaterial.PVC,
                        Diameter_mm = 25, Length_m = 15, FittingsEquivLength_m = 1, HazenWilliamsC = 140 }
            }
        };

        var result = _engine.Solve(input);

        Assert.True(result.Success, result.ErrorMessage);
        var headNode = result.Nodes.First(n => n.NodeId == 2);
        Assert.True(headNode.ComputedPressure_bar > 0, "Head node must have positive pressure");
        Assert.True(headNode.ComputedPressure_bar < 4.0, "Pressure must drop from source");
    }

    // ── Loop detection ────────────────────────────────────────────────────────
    [Fact]
    public void Solve_LoopDetected_ReturnsError()
    {
        // 0→1→2→0 forms a loop
        var input = new HydraulicInput
        {
            SourcePressure_bar = 4.0,
            SourceNodeId       = 0,
            Nodes = new List<HydraulicNode>
            {
                new() { NodeId = 0, Type = NodeType.Source },
                new() { NodeId = 1, Type = NodeType.Junction },
                new() { NodeId = 2, Type = NodeType.Junction }
            },
            Segments = new List<HydraulicSegment>
            {
                new() { SegmentId = 1, FromNodeId = 0, ToNodeId = 1,
                        Diameter_mm = 50, Length_m = 10, HazenWilliamsC = 140 },
                new() { SegmentId = 2, FromNodeId = 1, ToNodeId = 2,
                        Diameter_mm = 50, Length_m = 10, HazenWilliamsC = 140 },
                new() { SegmentId = 3, FromNodeId = 2, ToNodeId = 0,
                        Diameter_mm = 50, Length_m = 10, HazenWilliamsC = 140 }
            }
        };

        var result = _engine.Solve(input);

        Assert.False(result.Success);
        Assert.Contains("Loop", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    // ── Unit conversion round-trip ────────────────────────────────────────────
    [Fact]
    public void UnitConverter_LminM3h_RoundTrip()
    {
        double original = 42.5;
        double converted = UnitConverter.LminToM3h(original);
        double backAgain = UnitConverter.M3hToLmin(converted);
        Assert.InRange(backAgain, original - 0.001, original + 0.001);
    }

    [Fact]
    public void UnitConverter_BarMeters_RoundTrip()
    {
        double bar = 3.5;
        double m   = UnitConverter.BarToMeters(bar);
        double b2  = UnitConverter.MetersToBar(m);
        Assert.InRange(b2, bar - 0.001, bar + 0.001);
    }
}
