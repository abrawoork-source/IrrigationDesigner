// File: src/IrrigationCalc/Validation/EvaluationEngine.cs
using IrrigationCalc.Calculations;
using IrrigationCalc.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IrrigationCalc.Validation;

/// <summary>
/// Runs all design checks and returns a rated EvaluationResult.
/// </summary>
public class EvaluationEngine
{
    private readonly ILogger<EvaluationEngine> _logger;
    public EvaluationThresholds Thresholds { get; set; } = new();

    public EvaluationEngine(
        EvaluationThresholds? thresholds = null,
        ILogger<EvaluationEngine>? logger = null)
    {
        Thresholds = thresholds ?? new EvaluationThresholds();
        _logger    = logger ?? NullLogger<EvaluationEngine>.Instance;
    }

    public EvaluationResult Evaluate(
        IEnumerable<ZoneCalcResult>  zoneResults,
        HydraulicResult?             hydraulicResult,
        double                       availableFlow_Lmin,
        double                       sourcePressure_bar)
    {
        var issues = new List<DesignIssue>();

        // ── Zone-level checks ────────────────────────────────────────────────
        foreach (var zone in zoneResults)
        {
            // Warnings carried from ZoneCalculator
            foreach (var w in zone.Warnings)
                issues.Add(new DesignIssue
                {
                    Severity      = IssueSeverity.Warning,
                    Code          = "ZONE-WARN",
                    Message       = w,
                    ZoneId        = zone.ZoneId,
                    ZoneName      = zone.ZoneName,
                    SuggestedFix  = "Review zone configuration."
                });

            // PR out of typical range (5-25 mm/hr for spray)
            if (zone.PR_mmhr > 0)
            {
                if (zone.PR_mmhr > 25)
                    issues.Add(new DesignIssue
                    {
                        Severity     = IssueSeverity.Warning,
                        Code         = "PR-HIGH",
                        Message      = $"Zone '{zone.ZoneName}': precipitation rate {zone.PR_mmhr:F1} mm/hr is very high (>25 mm/hr). Risk of runoff.",
                        ZoneId       = zone.ZoneId,
                        ZoneName     = zone.ZoneName,
                        SuggestedFix = "Reduce head flow or increase zone area."
                    });

                if (zone.PR_mmhr < 3 && zone.HeadCount > 0)
                    issues.Add(new DesignIssue
                    {
                        Severity     = IssueSeverity.Info,
                        Code         = "PR-LOW",
                        Message      = $"Zone '{zone.ZoneName}': precipitation rate {zone.PR_mmhr:F1} mm/hr is very low (<3 mm/hr).",
                        ZoneId       = zone.ZoneId,
                        ZoneName     = zone.ZoneName,
                        SuggestedFix = "Verify nozzle selection and spacing."
                    });
            }

            // Flow exceeds source supply
            if (zone.TotalFlow_Lmin > availableFlow_Lmin * 0.9)
                issues.Add(new DesignIssue
                {
                    Severity     = IssueSeverity.Error,
                    Code         = "FLOW-EXCEED",
                    Message      = $"Zone '{zone.ZoneName}': total flow {zone.TotalFlow_Lmin:F1} L/min exceeds 90% of available supply ({availableFlow_Lmin:F1} L/min).",
                    ZoneId       = zone.ZoneId,
                    ZoneName     = zone.ZoneName,
                    SuggestedFix = "Reduce heads per zone or increase water supply."
                });

            // Runtime too long (> 4 hours)
            if (zone.Runtime_min > 240)
                issues.Add(new DesignIssue
                {
                    Severity     = IssueSeverity.Warning,
                    Code         = "RUNTIME-LONG",
                    Message      = $"Zone '{zone.ZoneName}': runtime {zone.Runtime_min:F0} min exceeds 4 hours.",
                    ZoneId       = zone.ZoneId,
                    ZoneName     = zone.ZoneName,
                    SuggestedFix = "Increase PR or reduce target depth."
                });
        }

        // ── PR mismatch between zones ────────────────────────────────────────
        var validZones = zoneResults.Where(z => z.PR_mmhr > 0).ToList();
        if (validZones.Count >= 2)
        {
            double maxPR = validZones.Max(z => z.PR_mmhr);
            double minPR = validZones.Min(z => z.PR_mmhr);
            if (minPR > 0)
            {
                double mismatch = (maxPR - minPR) / minPR * 100.0;
                if (mismatch > Thresholds.MaxPRMismatch_percent)
                    issues.Add(new DesignIssue
                    {
                        Severity     = IssueSeverity.Warning,
                        Code         = "PR-MISMATCH",
                        Message      = $"Precipitation rate mismatch between zones: {mismatch:F1}% (threshold {Thresholds.MaxPRMismatch_percent}%). Scheduling will be difficult.",
                        SuggestedFix = "Try to match PR across zones by selecting compatible nozzles."
                    });
            }
        }

        // ── Hydraulics checks ────────────────────────────────────────────────
        if (hydraulicResult != null)
        {
            if (!hydraulicResult.Success)
                issues.Add(new DesignIssue
                {
                    Severity     = IssueSeverity.Error,
                    Code         = "HYD-FAIL",
                    Message      = $"Hydraulic analysis failed: {hydraulicResult.ErrorMessage}",
                    SuggestedFix = "Check pipe network topology for loops or disconnected nodes."
                });

            foreach (var w in hydraulicResult.Warnings)
                issues.Add(new DesignIssue
                {
                    Severity     = IssueSeverity.Warning,
                    Code         = "HYD-WARN",
                    Message      = w,
                    SuggestedFix = "Check pressure at head nodes."
                });

            // Velocity checks
            foreach (var seg in hydraulicResult.Segments)
            {
                double limit = seg.Diameter_mm <= 32
                    ? Thresholds.MaxLateralVelocity_ms
                    : Thresholds.MaxMainlineVelocity_ms;

                if (seg.Velocity_ms > limit)
                    issues.Add(new DesignIssue
                    {
                        Severity     = IssueSeverity.Error,
                        Code         = "VEL-HIGH",
                        SegmentId    = seg.SegmentId,
                        Message      = $"Segment {seg.SegmentId}: velocity {seg.Velocity_ms:F2} m/s exceeds limit {limit:F2} m/s (Ø{seg.Diameter_mm} mm).",
                        SuggestedFix = "Increase pipe diameter or reduce flow on this segment."
                    });
                else if (seg.Velocity_ms > limit * 0.8)
                    issues.Add(new DesignIssue
                    {
                        Severity     = IssueSeverity.Warning,
                        Code         = "VEL-WARN",
                        SegmentId    = seg.SegmentId,
                        Message      = $"Segment {seg.SegmentId}: velocity {seg.Velocity_ms:F2} m/s is approaching limit {limit:F2} m/s.",
                        SuggestedFix = "Consider upsizing the pipe."
                    });
            }

            // Pressure at head nodes
            foreach (var node in hydraulicResult.Nodes.Where(n => n.Type == NodeType.HeadNode))
            {
                if (node.ComputedPressure_bar < Thresholds.MinHeadPressure_bar)
                    issues.Add(new DesignIssue
                    {
                        Severity     = IssueSeverity.Error,
                        Code         = "PRESS-LOW",
                        NodeId       = node.NodeId,
                        Message      = $"Node {node.NodeId}: pressure {node.ComputedPressure_bar:F2} bar is below minimum {Thresholds.MinHeadPressure_bar:F2} bar.",
                        SuggestedFix = "Increase source pressure or reduce pipe losses."
                    });
                else if (node.ComputedPressure_bar > Thresholds.MaxHeadPressure_bar)
                    issues.Add(new DesignIssue
                    {
                        Severity     = IssueSeverity.Warning,
                        Code         = "PRESS-HIGH",
                        NodeId       = node.NodeId,
                        Message      = $"Node {node.NodeId}: pressure {node.ComputedPressure_bar:F2} bar exceeds maximum {Thresholds.MaxHeadPressure_bar:F2} bar.",
                        SuggestedFix = "Install pressure regulator or adjust design."
                    });
            }

            // Pressure variation at head nodes
            var headPressures = hydraulicResult.Nodes
                .Where(n => n.Type == NodeType.HeadNode)
                .Select(n => n.ComputedPressure_bar)
                .ToList();
            if (headPressures.Count >= 2)
            {
                double maxP = headPressures.Max();
                double minP = headPressures.Min();
                if (maxP > 0)
                {
                    double variation = (maxP - minP) / maxP * 100.0;
                    if (variation > Thresholds.MaxPressureVariation_percent)
                        issues.Add(new DesignIssue
                        {
                            Severity     = IssueSeverity.Warning,
                            Code         = "PRESS-VAR",
                            Message      = $"Head node pressure variation: {variation:F1}% (threshold {Thresholds.MaxPressureVariation_percent}%).",
                            SuggestedFix = "Balance pipe network or use pressure-compensating emitters."
                        });
                }
            }
        }
        else
        {
            issues.Add(new DesignIssue
            {
                Severity     = IssueSeverity.Info,
                Code         = "HYD-SKIP",
                Message      = "Hydraulic analysis not run or no pipe network defined.",
                SuggestedFix = "Define pipe nodes and segments to enable hydraulic analysis."
            });
        }

        // ── Overall rating ────────────────────────────────────────────────────
        var rating = issues.Any(i => i.Severity == IssueSeverity.Error)
            ? DesignRating.NotAcceptable
            : issues.Any(i => i.Severity == IssueSeverity.Warning)
                ? DesignRating.Acceptable
                : DesignRating.Good;

        _logger.LogInformation(
            "Evaluation complete: {Rating}. Errors={E}, Warnings={W}, Info={I}",
            rating, issues.Count(i => i.Severity == IssueSeverity.Error),
            issues.Count(i => i.Severity == IssueSeverity.Warning),
            issues.Count(i => i.Severity == IssueSeverity.Info));

        return new EvaluationResult { OverallRating = rating, Issues = issues };
    }
}
