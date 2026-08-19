// File: src/IrrigationCalc/Models/DomainModels.cs
namespace IrrigationCalc.Models;

public enum IrrigationMethod { Spray, MP, Drip }
public enum PipeMaterial { PVC, PE }
public enum NodeType { Source, Valve, Junction, HeadNode }
public enum IssueSeverity { Info, Warning, Error }
public enum DesignRating { Good, Acceptable, NotAcceptable }

public class ZoneCalcInput
{
    public int ZoneId { get; set; }
    public string ZoneName { get; set; } = "";
    public IrrigationMethod Method { get; set; }
    public double Area_m2 { get; set; }
    public double DesignPressure_bar { get; set; }
    public double TargetDepth_mm { get; set; }
    public List<HeadFlowInput> Heads { get; set; } = new();
}

public class HeadFlowInput
{
    public int HeadId { get; set; }
    public double Flow_Lmin { get; set; }
    public double NozzlePressure_bar { get; set; }
}

public class ZoneCalcResult
{
    public int ZoneId { get; set; }
    public string ZoneName { get; set; } = "";
    public double TotalFlow_Lmin { get; set; }
    public double TotalFlow_m3h { get; set; }
    public double PR_mmhr { get; set; }
    public double Runtime_min { get; set; }
    public int HeadCount { get; set; }
    public List<string> Warnings { get; set; } = new();
}

public class HydraulicNode
{
    public int NodeId { get; set; }
    public double Elevation_m { get; set; }
    public NodeType Type { get; set; }
    public double DemandFlow_Lmin { get; set; }
    public double ComputedPressure_bar { get; set; }
    public List<int> ChildNodeIds { get; set; } = new();
}

public class HydraulicSegment
{
    public int SegmentId { get; set; }
    public int FromNodeId { get; set; }
    public int ToNodeId { get; set; }
    public PipeMaterial Material { get; set; }
    public double Diameter_mm { get; set; }
    public double Length_m { get; set; }
    public double FittingsEquivLength_m { get; set; }
    public double HazenWilliamsC { get; set; } = 140; // PVC default
    // Results
    public double Flow_Lmin { get; set; }
    public double Velocity_ms { get; set; }
    public double HeadLoss_m { get; set; }
    public double PressureDrop_bar { get; set; }
}

public class HydraulicInput
{
    public double SourcePressure_bar { get; set; }
    public double SourceElevation_m { get; set; }
    public int SourceNodeId { get; set; }
    public List<HydraulicNode> Nodes { get; set; } = new();
    public List<HydraulicSegment> Segments { get; set; } = new();
    public Dictionary<int, double> HeadNodeRequiredPressure { get; set; } = new(); // nodeId -> bar
}

public class HydraulicResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public List<HydraulicNode> Nodes { get; set; } = new();
    public List<HydraulicSegment> Segments { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public class DesignIssue
{
    public IssueSeverity Severity { get; set; }
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public string SuggestedFix { get; set; } = "";
    public string? ZoneName { get; set; }
    public int? ZoneId { get; set; }
    public int? SegmentId { get; set; }
    public int? NodeId { get; set; }
}

public class EvaluationResult
{
    public DesignRating OverallRating { get; set; }
    public List<DesignIssue> Issues { get; set; } = new();
    public int ErrorCount => Issues.Count(i => i.Severity == IssueSeverity.Error);
    public int WarningCount => Issues.Count(i => i.Severity == IssueSeverity.Warning);
    public int InfoCount => Issues.Count(i => i.Severity == IssueSeverity.Info);
}

public class EvaluationThresholds
{
    public double MaxLateralVelocity_ms { get; set; } = 1.5;
    public double MaxMainlineVelocity_ms { get; set; } = 2.0;
    public double MinHeadPressure_bar { get; set; } = 1.5;
    public double MaxHeadPressure_bar { get; set; } = 4.5;
    public double MaxPRMismatch_percent { get; set; } = 20.0;
    public double MaxPressureVariation_percent { get; set; } = 10.0;
}

public class UnitConversionResult
{
    public double Value { get; set; }
    public string Unit { get; set; } = "";
}
