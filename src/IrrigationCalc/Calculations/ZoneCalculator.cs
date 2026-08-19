// File: src/IrrigationCalc/Calculations/ZoneCalculator.cs
using IrrigationCalc.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace IrrigationCalc.Calculations;

/// <summary>
/// Calculates zone-level totals: total flow, precipitation rate, and runtime.
/// </summary>
public class ZoneCalculator
{
    private readonly ILogger<ZoneCalculator> _logger;

    public ZoneCalculator(ILogger<ZoneCalculator>? logger = null)
    {
        _logger = logger ?? NullLogger<ZoneCalculator>.Instance;
    }

    /// <summary>
    /// Calculate totals for a single zone.
    /// </summary>
    public ZoneCalcResult Calculate(ZoneCalcInput input)
    {
        if (input == null) throw new ArgumentNullException(nameof(input));

        var result = new ZoneCalcResult
        {
            ZoneId   = input.ZoneId,
            ZoneName = input.ZoneName,
            HeadCount = input.Heads.Count
        };

        // ── Total flow ────────────────────────────────────────────────────────
        result.TotalFlow_Lmin = input.Heads.Sum(h => h.Flow_Lmin);
        result.TotalFlow_m3h  = UnitConverter.LminToM3h(result.TotalFlow_Lmin);

        // ── Precipitation rate ────────────────────────────────────────────────
        // PR (mm/hr) = (60 * Q [L/min]) / Area [m²]
        // Derivation: Q [L/min] * 60 = [L/hr];  [L/hr] / [m²] = [mm/hr] (since 1L/m² = 1mm)
        if (input.Area_m2 <= 0)
        {
            result.Warnings.Add("Zone area is zero or negative; PR and runtime cannot be computed.");
            _logger.LogWarning("Zone {ZoneId} has non-positive area {Area}", input.ZoneId, input.Area_m2);
            return result;
        }

        result.PR_mmhr = (60.0 * result.TotalFlow_Lmin) / input.Area_m2;

        // ── Runtime ───────────────────────────────────────────────────────────
        // runtime (min) = (TargetDepth [mm] / PR [mm/hr]) * 60
        if (result.PR_mmhr <= 0)
        {
            result.Warnings.Add("Precipitation rate is zero; runtime cannot be computed (check head flows).");
            return result;
        }

        if (input.TargetDepth_mm <= 0)
        {
            result.Warnings.Add("Target depth is zero or negative; runtime defaults to 0.");
            result.Runtime_min = 0;
        }
        else
        {
            result.Runtime_min = (input.TargetDepth_mm / result.PR_mmhr) * 60.0;
        }

        // ── Sanity checks ─────────────────────────────────────────────────────
        if (result.TotalFlow_Lmin == 0)
            result.Warnings.Add("Zone has no heads or all heads have zero flow.");

        if (result.HeadCount == 0)
            result.Warnings.Add("Zone has no heads assigned.");

        _logger.LogDebug(
            "Zone {ZoneId}: Flow={Flow:F2} L/min, PR={PR:F2} mm/hr, Runtime={RT:F1} min",
            input.ZoneId, result.TotalFlow_Lmin, result.PR_mmhr, result.Runtime_min);

        return result;
    }

    /// <summary>
    /// Calculate totals for multiple zones.
    /// </summary>
    public List<ZoneCalcResult> CalculateAll(IEnumerable<ZoneCalcInput> zones)
    {
        var results = new List<ZoneCalcResult>();
        foreach (var z in zones)
            results.Add(Calculate(z));
        return results;
    }
}
