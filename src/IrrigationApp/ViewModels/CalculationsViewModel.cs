// File: src/IrrigationApp/ViewModels/CalculationsViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using IrrigationCalc.Calculations;
using IrrigationCalc.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace IrrigationApp.ViewModels;

public class CalculationsViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public Project? Project { get; }

    // ── Zone results ──────────────────────────────────────────────────────────
    public ObservableCollection<ZoneResultRow> ZoneResults { get; } = new();

    // ── Hydraulic results ─────────────────────────────────────────────────────
    public ObservableCollection<SegmentResultRow> SegmentResults { get; } = new();
    public ObservableCollection<NodeResultRow>    NodeResults    { get; } = new();

    private string _hydraulicsStatus = "Not calculated";
    public string HydraulicsStatus
    {
        get => _hydraulicsStatus;
        set => SetProperty(ref _hydraulicsStatus, value);
    }

    private string _status = "";
    public string StatusMessage { get => _status; set => SetProperty(ref _status, value); }

    // ── Localization ──────────────────────────────────────────────────────────
    public string L_Title      => _loc.Get("Nav_Calculations");
    public string L_Calculate  => _loc.Get("Btn_Calculate");
    public string L_ZoneName   => _loc.Get("Lbl_ZoneName");
    public string L_HeadCount  => _loc.Get("Lbl_HeadCount");
    public string L_TotalFlow  => _loc.Get("Lbl_TotalFlow");
    public string L_PR         => _loc.Get("Lbl_PR");
    public string L_Runtime    => _loc.Get("Lbl_Runtime");
    public string L_Segment    => "Segment";
    public string L_FromNode   => _loc.Get("Lbl_FromNode");
    public string L_ToNode     => _loc.Get("Lbl_ToNode");
    public string L_Diameter   => _loc.Get("Lbl_Diameter");
    public string L_Flow       => _loc.Get("Lbl_Flow");
    public string L_Velocity   => _loc.Get("Lbl_Velocity");
    public string L_HeadLoss   => _loc.Get("Lbl_HeadLoss");
    public string L_PressDrop  => _loc.Get("Lbl_PressureDrop");
    public string L_NodeId     => "Node ID";
    public string L_NodeType   => _loc.Get("Lbl_NodeType");
    public string L_NodePress  => _loc.Get("Lbl_NodePressure");

    public AsyncRelayCommand CalculateCommand { get; }

    public CalculationsViewModel(DatabaseService db, Project? project)
    {
        _db     = db;
        Project = project;
        CalculateCommand = new AsyncRelayCommand(CalculateAsync);
    }

    private async Task CalculateAsync()
    {
        if (Project == null) { StatusMessage = _loc.Get("Msg_NoProject"); return; }

        await using var ctx = _db.CreateContext();
        var project = await ctx.Projects
            .Include(p => p.WaterSource)
            .Include(p => p.Zones)
            .Include(p => p.Heads).ThenInclude(h => h.Nozzle)
            .Include(p => p.PipeNodes)
            .Include(p => p.PipeSegments)
            .FirstOrDefaultAsync(p => p.Id == Project.Id);

        if (project == null) return;

        // ── Zone calculations ─────────────────────────────────────────────────
        var zoneInputs = project.Zones.Select(z => new ZoneCalcInput
        {
            ZoneId             = z.Id,
            ZoneName           = z.Name,
            Method             = (IrrigationMethod)z.Method,
            Area_m2            = z.Area_m2,
            DesignPressure_bar = z.DesignPressure_bar,
            TargetDepth_mm     = z.TargetDepth_mm,
            Heads = project.Heads.Where(h => h.ZoneId == z.Id)
                .Select(h => new HeadFlowInput
                {
                    HeadId             = h.Id,
                    Flow_Lmin          = h.Nozzle?.Flow_Lmin ?? 0,
                    NozzlePressure_bar = h.Nozzle?.Pressure_bar ?? z.DesignPressure_bar
                }).ToList()
        }).ToList();

        var calc = new ZoneCalculator();
        var zResults = calc.CalculateAll(zoneInputs);

        ZoneResults.Clear();
        foreach (var r in zResults)
            ZoneResults.Add(new ZoneResultRow(r));

        // ── Hydraulics ────────────────────────────────────────────────────────
        if (!project.PipeNodes.Any() || !project.PipeSegments.Any())
        {
            HydraulicsStatus = "No pipe network defined.";
            StatusMessage = _loc.Get("Msg_CalculationDone");
            return;
        }

        var srcNode = project.PipeNodes.FirstOrDefault(n => n.Type == NodeTypeDb.Source)
                      ?? project.PipeNodes.First();

        var hydInput = new HydraulicInput
        {
            SourcePressure_bar = project.WaterSource?.StaticPressure_bar ?? 4.0,
            SourceElevation_m  = project.WaterSource?.Elevation_m ?? 0,
            SourceNodeId       = srcNode.Id,
            Nodes = project.PipeNodes.Select(n => new HydraulicNode
            {
                NodeId          = n.Id,
                Elevation_m     = n.Elevation_m,
                Type            = (IrrigationCalc.Models.NodeType)n.Type,
                DemandFlow_Lmin = n.Type == NodeTypeDb.HeadNode
                    ? zResults.Sum(z => z.TotalFlow_Lmin) /
                      Math.Max(1, project.PipeNodes.Count(p => p.Type == NodeTypeDb.HeadNode))
                    : 0
            }).ToList(),
            Segments = project.PipeSegments.Select(s => new HydraulicSegment
            {
                SegmentId             = s.Id,
                FromNodeId            = s.FromNodeId,
                ToNodeId              = s.ToNodeId,
                Material              = (IrrigationCalc.Models.PipeMaterial)s.Material,
                Diameter_mm           = s.Diameter_mm,
                Length_m              = s.Length_m,
                FittingsEquivLength_m = s.FittingsEquivLength_m
            }).ToList()
        };

        var engine = new HydraulicsEngine();
        var hydResult = engine.Solve(hydInput);

        HydraulicsStatus = hydResult.Success
            ? $"✅ Success – {hydResult.Warnings.Count} warnings"
            : $"❌ Failed: {hydResult.ErrorMessage}";

        SegmentResults.Clear();
        foreach (var seg in hydResult.Segments)
            SegmentResults.Add(new SegmentResultRow(seg));

        NodeResults.Clear();
        foreach (var node in hydResult.Nodes)
            NodeResults.Add(new NodeResultRow(node));

        StatusMessage = _loc.Get("Msg_CalculationDone");
    }
}

public class ZoneResultRow
{
    public string ZoneName    { get; }
    public int    HeadCount   { get; }
    public string TotalFlow   { get; }
    public string PR          { get; }
    public string Runtime     { get; }
    public List<string> Warnings { get; }

    public ZoneResultRow(ZoneCalcResult r)
    {
        ZoneName  = r.ZoneName;
        HeadCount = r.HeadCount;
        TotalFlow = $"{r.TotalFlow_Lmin:F2} L/min ({r.TotalFlow_m3h:F3} m³/h)";
        PR        = $"{r.PR_mmhr:F2} mm/hr";
        Runtime   = $"{r.Runtime_min:F1} min";
        Warnings  = r.Warnings;
    }
}

public class SegmentResultRow
{
    public int    SegmentId    { get; }
    public int    FromNodeId   { get; }
    public int    ToNodeId     { get; }
    public string Diameter     { get; }
    public string Flow         { get; }
    public string Velocity     { get; }
    public string HeadLoss     { get; }
    public string PressureDrop { get; }

    public SegmentResultRow(HydraulicSegment s)
    {
        SegmentId    = s.SegmentId;
        FromNodeId   = s.FromNodeId;
        ToNodeId     = s.ToNodeId;
        Diameter     = $"{s.Diameter_mm:F0} mm";
        Flow         = $"{s.Flow_Lmin:F2} L/min";
        Velocity     = $"{s.Velocity_ms:F2} m/s";
        HeadLoss     = $"{s.HeadLoss_m:F3} m";
        PressureDrop = $"{s.PressureDrop_bar:F4} bar";
    }
}

public class NodeResultRow
{
    public int    NodeId   { get; }
    public string NodeType { get; }
    public string Pressure { get; }

    public NodeResultRow(HydraulicNode n)
    {
        NodeId   = n.NodeId;
        NodeType = n.Type.ToString();
        Pressure = $"{n.ComputedPressure_bar:F3} bar";
    }
}
