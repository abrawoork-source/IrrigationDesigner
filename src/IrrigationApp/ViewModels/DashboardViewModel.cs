// File: src/IrrigationApp/ViewModels/DashboardViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using IrrigationCalc.Calculations;
using IrrigationCalc.Models;
using IrrigationCalc.Validation;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace IrrigationApp.ViewModels;

public class DashboardViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public Project? Project { get; }

    // ── Summary cards ─────────────────────────────────────────────────────────
    public string ProjectName   => Project?.Name ?? _loc.Get("Dashboard_NoProject");
    public int ZoneCount        { get; private set; }
    public int HeadCount        { get; private set; }
    public int SegmentCount     { get; private set; }
    public double TotalFlow_Lmin{ get; private set; }
    public double AvailFlow_Lmin{ get; private set; }

    // ── Design check dashboard ────────────────────────────────────────────────
    private DesignRating _rating = DesignRating.Good;
    public DesignRating Rating
    {
        get => _rating;
        private set
        {
            SetProperty(ref _rating, value);
            OnPropertyChanged(nameof(RatingColor));
            OnPropertyChanged(nameof(RatingText));
            OnPropertyChanged(nameof(RatingEmoji));
        }
    }

    public Brush RatingColor => Rating switch
    {
        DesignRating.Good           => new SolidColorBrush(Color.FromRgb(34, 197, 94)),
        DesignRating.Acceptable     => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
        DesignRating.NotAcceptable  => new SolidColorBrush(Color.FromRgb(239, 68, 68)),
        _                           => Brushes.Gray
    };

    public string RatingText => Rating switch
    {
        DesignRating.Good          => _loc.Get("Rating_Good"),
        DesignRating.Acceptable    => _loc.Get("Rating_Acceptable"),
        DesignRating.NotAcceptable => _loc.Get("Rating_NotAcceptable"),
        _                          => "–"
    };

    public string RatingEmoji => Rating switch
    {
        DesignRating.Good          => "✅",
        DesignRating.Acceptable    => "⚠",
        DesignRating.NotAcceptable => "❌",
        _                          => "?"
    };

    public int ErrorCount   { get; private set; }
    public int WarningCount { get; private set; }
    public int InfoCount    { get; private set; }

    public ObservableCollection<IssueRow> Issues { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────
    public AsyncRelayCommand RunDesignCheckCommand { get; }

    // ── Localization ──────────────────────────────────────────────────────────
    public string L_Title     => _loc.Get("Dashboard_Title");
    public string L_Zones     => _loc.Get("Dashboard_Zones");
    public string L_Heads     => _loc.Get("Dashboard_Heads");
    public string L_Segments  => _loc.Get("Dashboard_Segments");
    public string L_Status    => _loc.Get("Dashboard_Status");
    public string L_DesignCheck => _loc.Get("Btn_DesignCheck");
    public string L_Rating    => _loc.Get("Lbl_Rating");

    public DashboardViewModel(DatabaseService db, Project? project)
    {
        _db     = db;
        Project = project;
        RunDesignCheckCommand = new AsyncRelayCommand(RunDesignCheckAsync);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Project == null) return;
        await using var ctx = _db.CreateContext();

        ZoneCount    = await ctx.Zones.CountAsync(z => z.ProjectId == Project.Id);
        HeadCount    = await ctx.Heads.CountAsync(h => h.ProjectId == Project.Id);
        SegmentCount = await ctx.PipeSegments.CountAsync(s => s.ProjectId == Project.Id);

        var ws = await ctx.WaterSources.FirstOrDefaultAsync(w => w.ProjectId == Project.Id);
        AvailFlow_Lmin = ws?.AvailableFlow_Lmin ?? 0;

        var heads = await ctx.Heads.Include(h => h.Nozzle)
            .Where(h => h.ProjectId == Project.Id).ToListAsync();
        TotalFlow_Lmin = heads.Sum(h => h.Nozzle?.Flow_Lmin ?? 0);

        OnPropertyChanged(nameof(ZoneCount));
        OnPropertyChanged(nameof(HeadCount));
        OnPropertyChanged(nameof(SegmentCount));
        OnPropertyChanged(nameof(TotalFlow_Lmin));
        OnPropertyChanged(nameof(AvailFlow_Lmin));
        OnPropertyChanged(nameof(ProjectName));

        await RunDesignCheckAsync();
    }

    private async Task RunDesignCheckAsync()
    {
        if (Project == null) { Rating = DesignRating.Good; return; }

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
            ZoneId         = z.Id,
            ZoneName       = z.Name,
            Method         = (IrrigationCalc.Models.IrrigationMethod)z.Method,
            Area_m2        = z.Area_m2,
            DesignPressure_bar = z.DesignPressure_bar,
            TargetDepth_mm = z.TargetDepth_mm,
            Heads          = project.Heads.Where(h => h.ZoneId == z.Id)
                .Select(h => new HeadFlowInput
                {
                    HeadId             = h.Id,
                    Flow_Lmin          = h.Nozzle?.Flow_Lmin ?? 0,
                    NozzlePressure_bar = h.Nozzle?.Pressure_bar ?? z.DesignPressure_bar
                }).ToList()
        }).ToList();

        var zoneCalc   = new ZoneCalculator();
        var zoneResults = zoneCalc.CalculateAll(zoneInputs);

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
                    NodeId      = n.Id,
                    Elevation_m = n.Elevation_m,
                    Type        = (IrrigationCalc.Models.NodeType)n.Type,
                    DemandFlow_Lmin = n.Type == NodeTypeDb.HeadNode
                        ? project.Heads.Where(h => h.ZoneId > 0)
                            .Sum(h => h.Nozzle?.Flow_Lmin ?? 0) / Math.Max(1, project.PipeNodes.Count(p => p.Type == NodeTypeDb.HeadNode))
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
            hydResult = engine.Solve(hydInput);
        }

        var evalEngine = new EvaluationEngine();
        var evalResult = evalEngine.Evaluate(zoneResults, hydResult,
            project.WaterSource?.AvailableFlow_Lmin ?? 999,
            project.WaterSource?.StaticPressure_bar ?? 4.0);

        Rating       = evalResult.OverallRating;
        ErrorCount   = evalResult.ErrorCount;
        WarningCount = evalResult.WarningCount;
        InfoCount    = evalResult.InfoCount;

        Issues.Clear();
        foreach (var issue in evalResult.Issues)
            Issues.Add(new IssueRow(issue));

        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(InfoCount));
    }
}

public class IssueRow
{
    public string SeverityEmoji { get; }
    public string Code          { get; }
    public string Message       { get; }
    public string SuggestedFix  { get; }
    public Brush  SeverityColor { get; }

    public IssueRow(IrrigationCalc.Models.DesignIssue issue)
    {
        Code         = issue.Code;
        Message      = issue.Message;
        SuggestedFix = issue.SuggestedFix;
        SeverityEmoji = issue.Severity switch
        {
            IssueSeverity.Error   => "❌",
            IssueSeverity.Warning => "⚠",
            _                     => "ℹ"
        };
        SeverityColor = issue.Severity switch
        {
            IssueSeverity.Error   => new SolidColorBrush(Color.FromRgb(239, 68,  68)),
            IssueSeverity.Warning => new SolidColorBrush(Color.FromRgb(234, 179, 8)),
            _                     => new SolidColorBrush(Color.FromRgb(59,  130, 246))
        };
    }
}
