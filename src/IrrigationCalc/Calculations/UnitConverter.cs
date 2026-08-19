// File: src/IrrigationCalc/Calculations/UnitConverter.cs
namespace IrrigationCalc.Calculations;

/// <summary>
/// Static helper for unit conversions used throughout the calculation engine.
/// </summary>
public static class UnitConverter
{
    // ── Flow ──────────────────────────────────────────────────────────────────
    public static double LminToM3h(double lmin) => lmin * 0.06;
    public static double M3hToLmin(double m3h) => m3h / 0.06;
    public static double LminToM3s(double lmin) => lmin / 60_000.0;
    public static double M3sToLmin(double m3s) => m3s * 60_000.0;
    public static double LphToLmin(double lph) => lph / 60.0;
    public static double LminToLph(double lmin) => lmin * 60.0;

    // ── Pressure ──────────────────────────────────────────────────────────────
    public static double BarToKpa(double bar) => bar * 100.0;
    public static double KpaToBar(double kpa) => kpa / 100.0;
    public static double BarToPsi(double bar) => bar * 14.5038;
    public static double PsiToBar(double psi) => psi / 14.5038;
    public static double MetersToBar(double meters) => meters / 10.197;
    public static double BarToMeters(double bar) => bar * 10.197;

    // ── Length / Diameter ─────────────────────────────────────────────────────
    public static double MmToM(double mm) => mm / 1000.0;
    public static double MToMm(double m) => m * 1000.0;

    // ── Area ──────────────────────────────────────────────────────────────────
    public static double M2ToHa(double m2) => m2 / 10_000.0;
    public static double HaToM2(double ha) => ha * 10_000.0;
}
