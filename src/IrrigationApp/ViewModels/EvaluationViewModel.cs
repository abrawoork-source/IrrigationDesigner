// File: src/IrrigationApp/ViewModels/EvaluationViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using IrrigationCalc.Calculations;
using IrrigationCalc.Models;
using IrrigationCalc.Validation;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace IrrigationApp.ViewModels;

public class EvaluationViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public Project? Project { get; }

    // ── Rating dashboard ──────────────────────────────────────────────────────
    private DesignRating _rating = DesignRating.Good;
    public DesignRating Rating
    {
        get => _rating;
        set
        {
            SetProperty(ref _rating, value);
            OnPropertyChanged(nameof(RatingColor));
            OnPropertyChanged(nameof(RatingText));
            OnPropertyChanged(nameof(RatingBadgeColor));
        }
    }

    public Brush RatingColor => Rating switch
    {
        DesignRating.Good          => new SolidColorBrush(Color.FromRgb(34,  197, 94)),
        DesignRating.Acceptable    => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
        DesignRating.NotAcceptable => new SolidColorBrush(Color.FromRgb(239, 68,  68)),
        _                          => Brushes.Gray
    };

    public Brush RatingBadgeColor => Rating switch
    {
        DesignRating.Good          => new SolidColorBrush(Color.FromRgb(21,  128, 61)),
        DesignRating.Acceptable    => new SolidColorBrush(Color.FromRgb(161, 98,  7)),
        DesignRating.NotAcceptable => new SolidColorBrush(Color.FromRgb(185, 28,  28)),
        _                          => Brushes.Gray
    };

    public string RatingText => Rating switch
    {
        DesignRating.Good          => _loc.Get("Rating_Good"),
        DesignRating.Acceptable    => _loc.Get("Rating_Acceptable"),
        DesignRating.NotAcceptable => _loc.Get("Rating_NotAcceptable"),
        _                          => "–"
    };

    private int _errorCount;   public int ErrorCount   { get => _errorCount;   set => SetProperty(ref _errorCount, value); }
    private int _warningCount; public int WarningCount { get => _warningCount; set => SetProperty(ref _warningCount, value); }
    private int _infoCount;    public int InfoCount    { get => _infoCount;    set => SetProperty(ref _infoCount, value); }

    public ObservableCollection<IssueRow> Issues { get; } = new();

    // ── Thresholds (configurable) ─────────────────────────────────────────────
    public double Threshold_MaxLateralVelocity  { get; set; } = 1.5;
    public double Threshold_MaxMainlineVelocity { get; set; } = 2.0;
    public double Threshold_MinHeadPressure     { get; set; } = 1.5;
    public double Threshold_MaxHeadPressure     { get; set; } = 4.5;
    public double Threshold_MaxPRMismatch       { get; set; } = 20.0;
    public double Threshold_MaxPressureVariation{ get; set; } = 10.0;

    private string _status = "";
    public string StatusMessage { get => _status; set => SetProperty(ref _status, value); }

    private bool _isCalculated = false;
    public bool IsCalculated { get => _isCalculated; set => SetProperty(ref _isCalculated, value); }

    // ── Localization ──────────────────────────────────────────────────────────
    public string L_Title        => _loc.Get("Nav_Evaluation");
    public string L_DesignCheck  => _loc.Get("Btn_DesignCheck");
    public string L_Rating       => _loc.Get("Lbl_Rating");
    public string L_Issues       => _loc.Get("Lbl_Issues");
    public string L_Severity     => _loc.Get("Lbl_Severity");
    public string L_Message      => _loc.Get("Lbl_Message");
    public string L_SuggestedFix => _loc.Get("Lbl_SuggestedFix");
    public string L_Thresholds   => "Evaluation Thresholds";
    public string L_MaxLatVel    => "Max Lateral Velocity (m/s)";
    public string L_MaxMainVel   => "Max Mainline Velocity (m/s)";
    public string L_MinHeadPres  => "Min Head Pressure (bar)";
    public string L_MaxHeadPres  => "Max Head Pressure (bar)";
    public string L_MaxPRMis     => "Max PR Mismatch (%)";
    public string L_MaxPresVar   => "Max Pressure Variation (%)";
    public string L_Errors       => "Errors";
    public string L_Warnings     => "Warnings";
    public string L_Info         => "Info";

    public AsyncRelayCommand RunCheckCommand { get; }

    public EvaluationViewModel(DatabaseService db, Project? project)
    {
        _db     = db;
        Project = project;
        RunCheckCommand = new AsyncRelayCommand(RunCheckAsync);
    }

    private async Task RunCheckAsync()
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

        // Zone calculations
        var zoneInputs = project.Zones.Select(z => new ZoneCalcInput
        {
            ZoneId = z.Id, ZoneName = z.Name,
            Method = (IrrigationMethod)z.Method,
            Area_m2 = z.Area_m2,
            DesignPressure_bar = z.DesignPressure_bar,
            TargetDepth_mm = z.TargetDepth_mm,
            Heads = project.Heads.Where(h => h.ZoneId == z.Id)
                .Select(h => new HeadFlowInput
                {
                    HeadId = h.Id,
                    Flow_Lmin = h.Nozzle?.Flow_Lmin ?? 0,
                    NozzlePressure_bar = h.Nozzle?.Pressure_bar ?? z.DesignPressure_bar
                }).ToList()
        }).ToList();

        var zoneResults = new ZoneCalculator().CalculateAll(zoneInputs);

        // Hydraulics
        HydraulicResult? hydResult = null;
        if (project.PipeNodes.Any() && project.PipeSegments.Any())
        {
            var src = project.PipeNodes.FirstOrDefault(n => n.Type == NodeTypeDb.Source)
                      ?? project.PipeNodes.First();
            var hydInput = new HydraulicInput
            {
                SourcePressure_bar = project.WaterSource?.StaticPressure_bar ?? 4.0,
                SourceElevation_m  = project.WaterSource?.Elevation_m ?? 0,
                SourceNodeId       = src.Id,
                Nodes = project.PipeNodes.Select(n => new HydraulicNode
                {
                    NodeId          = n.Id,
                    Elevation_m     = n.Elevation_m,
                    Type            = (IrrigationCalc.Models.NodeType)n.Type,
                    DemandFlow_Lmin = n.Type == NodeTypeDb.HeadNode
                        ? zoneResults.Sum(z => z.TotalFlow_Lmin) /
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
            hydResult = new HydraulicsEngine().Solve(hydInput);
        }

        // Evaluation
        var thresholds = new EvaluationThresholds
        {
            MaxLateralVelocity_ms      = Threshold_MaxLateralVelocity,
            MaxMainlineVelocity_ms     = Threshold_MaxMainlineVelocity,
            MinHeadPressure_bar        = Threshold_MinHeadPressure,
            MaxHeadPressure_bar        = Threshold_MaxHeadPressure,
            MaxPRMismatch_percent      = Threshold_MaxPRMismatch,
            MaxPressureVariation_percent = Threshold_MaxPressureVariation
        };

        var evalEngine = new EvaluationEngine(thresholds);
        var evalResult = evalEngine.Evaluate(zoneResults, hydResult,
            project.WaterSource?.AvailableFlow_Lmin ?? 999,
            project.WaterSource?.StaticPressure_bar ?? 4.0);

        Rating       = evalResult.OverallRating;
        ErrorCount   = evalResult.ErrorCount;
        WarningCount = evalResult.WarningCount;
        InfoCount    = evalResult.InfoCount;
        IsCalculated = true;

        Issues.Clear();
        foreach (var issue in evalResult.Issues)
            Issues.Add(new IssueRow(issue));

        StatusMessage = $"Design check complete: {RatingText}";
    }
}
