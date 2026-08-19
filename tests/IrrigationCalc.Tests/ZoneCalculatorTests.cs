// File: tests/IrrigationCalc.Tests/ZoneCalculatorTests.cs
using IrrigationCalc.Calculations;
using IrrigationCalc.Models;
using Xunit;

namespace IrrigationCalc.Tests;

public class ZoneCalculatorTests
{
    private readonly ZoneCalculator _calc = new();

    private static ZoneCalcInput MakeInput(
        double area_m2, double targetDepth_mm,
        params double[] headFlows)
    {
        var heads = headFlows.Select((f, i) => new HeadFlowInput
        {
            HeadId             = i + 1,
            Flow_Lmin          = f,
            NozzlePressure_bar = 2.0
        }).ToList();

        return new ZoneCalcInput
        {
            ZoneId         = 1,
            ZoneName       = "TestZone",
            Method         = IrrigationMethod.Spray,
            Area_m2        = area_m2,
            TargetDepth_mm = targetDepth_mm,
            Heads          = heads
        };
    }

    // ── PR calculation ────────────────────────────────────────────────────────
    [Fact]
    public void PR_CorrectFormula()
    {
        // 4 heads × 10 L/min = 40 L/min; Area = 400 m²
        // PR = (60 × 40) / 400 = 6 mm/hr
        var input  = MakeInput(400, 10, 10, 10, 10, 10);
        var result = _calc.Calculate(input);

        Assert.InRange(result.PR_mmhr, 5.99, 6.01);
    }

    [Fact]
    public void PR_ZeroArea_ReturnsWarning()
    {
        var input  = MakeInput(0, 10, 10);
        var result = _calc.Calculate(input);

        Assert.Equal(0, result.PR_mmhr);
        Assert.NotEmpty(result.Warnings);
    }

    // ── Runtime calculation ───────────────────────────────────────────────────
    [Fact]
    public void Runtime_CorrectFormula()
    {
        // PR = 6 mm/hr; Target = 12 mm
        // Runtime = (12 / 6) * 60 = 120 min
        var input  = MakeInput(400, 12, 10, 10, 10, 10);
        var result = _calc.Calculate(input);

        Assert.InRange(result.Runtime_min, 119.9, 120.1);
    }

    [Fact]
    public void Runtime_ZeroTargetDepth_IsZero()
    {
        var input  = MakeInput(400, 0, 10, 10);
        var result = _calc.Calculate(input);

        Assert.Equal(0, result.Runtime_min);
        Assert.NotEmpty(result.Warnings);
    }

    // ── Total flow ────────────────────────────────────────────────────────────
    [Fact]
    public void TotalFlow_SumsCorrectly()
    {
        var input  = MakeInput(200, 10, 5, 7.5, 12.5);
        var result = _calc.Calculate(input);

        Assert.InRange(result.TotalFlow_Lmin, 24.99, 25.01);
    }

    [Fact]
    public void TotalFlow_m3h_ConversionCorrect()
    {
        var input  = MakeInput(100, 10, 10); // 10 L/min → 0.6 m3/h
        var result = _calc.Calculate(input);

        Assert.InRange(result.TotalFlow_m3h, 0.599, 0.601);
    }

    // ── No heads ──────────────────────────────────────────────────────────────
    [Fact]
    public void NoHeads_ZeroFlowAndWarning()
    {
        var input = new ZoneCalcInput
        {
            ZoneId = 1, ZoneName = "Empty", Area_m2 = 100, TargetDepth_mm = 10, Heads = new()
        };
        var result = _calc.Calculate(input);

        Assert.Equal(0, result.TotalFlow_Lmin);
        Assert.NotEmpty(result.Warnings);
    }
}
