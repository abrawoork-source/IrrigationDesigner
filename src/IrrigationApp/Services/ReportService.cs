// File: src/IrrigationApp/Services/ReportService.cs
using ClosedXML.Excel;
using IrrigationApp.Models;
using IrrigationCalc.Calculations;
using IrrigationCalc.Models;
using IrrigationCalc.Validation;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace IrrigationApp.Services;

/// <summary>
/// Generates Excel and PDF reports: ZoneSummary, HeadSchedule, Hydraulics, BOM.
/// </summary>
public class ReportService
{
    private readonly DatabaseService _db;
    private readonly ZoneCalculator  _zoneCalc = new();
    private readonly HydraulicsEngine _hydEngine = new();

    public ReportService(DatabaseService db)
    {
        _db = db;
        // Set QuestPDF community license
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ── Zone Summary Excel ────────────────────────────────────────────────────
    public async Task ExportZoneSummaryExcelAsync(int projectId, string filePath)
    {
        var (project, results) = await GetZoneResultsAsync(projectId);

        using var wb  = new XLWorkbook();
        var ws = wb.AddWorksheet("Zone Summary");
        ws.Cell(1,1).Value = $"Zone Summary – {project.Name}";
        ws.Cell(1,1).Style.Font.Bold     = true;
        ws.Cell(1,1).Style.Font.FontSize = 14;
        ws.Range(1,1,1,7).Merge();

        var headers = new[] { "Zone","Method","Area (m²)","Heads","Flow (L/min)","PR (mm/hr)","Runtime (min)" };
        for (int c = 0; c < headers.Length; c++)
        {
            ws.Cell(2, c+1).Value = headers[c];
            ws.Cell(2, c+1).Style.Font.Bold = true;
            ws.Cell(2, c+1).Style.Fill.BackgroundColor = XLColor.DarkBlue;
            ws.Cell(2, c+1).Style.Font.FontColor       = XLColor.White;
        }

        int row = 3;
        foreach (var r in results)
        {
            ws.Cell(row,1).Value = r.ZoneName;
            ws.Cell(row,2).Value = "Spray/MP/Drip";
            ws.Cell(row,3).Value = Math.Round(r.TotalFlow_Lmin, 2);
            ws.Cell(row,4).Value = r.HeadCount;
            ws.Cell(row,5).Value = Math.Round(r.TotalFlow_Lmin, 2);
            ws.Cell(row,6).Value = Math.Round(r.PR_mmhr, 2);
            ws.Cell(row,7).Value = Math.Round(r.Runtime_min, 1);
            row++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
    }

    // ── Zone Summary PDF ──────────────────────────────────────────────────────
    public async Task ExportZoneSummaryPdfAsync(int projectId, string filePath)
    {
        var (project, results) = await GetZoneResultsAsync(projectId);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Zone Summary – {project.Name}")
                        .FontSize(18).Bold().FontColor(Colors.Blue.Darken2);
                    col.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9).Italic();
                    col.Item().PaddingTop(10).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(2); c.RelativeColumn(); c.RelativeColumn();
                            c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                        });
                        static IContainer HeaderCell(IContainer c) =>
                            c.Background(Colors.Blue.Darken2).Padding(4).DefaultTextStyle(s => s.FontColor(Colors.White).Bold());
                        t.Header(h =>
                        {
                            foreach (var hdr in new[]{"Zone","Area (m²)","Heads","Flow (L/min)","PR (mm/hr)","Runtime (min)"})
                                h.Cell().Element(HeaderCell).Text(hdr);
                        });
                        foreach (var r in results)
                        {
                            t.Cell().Padding(4).Text(r.ZoneName);
                            t.Cell().Padding(4).AlignRight().Text("–");
                            t.Cell().Padding(4).AlignRight().Text(r.HeadCount.ToString());
                            t.Cell().Padding(4).AlignRight().Text($"{r.TotalFlow_Lmin:F2}");
                            t.Cell().Padding(4).AlignRight().Text($"{r.PR_mmhr:F2}");
                            t.Cell().Padding(4).AlignRight().Text($"{r.Runtime_min:F1}");
                        }
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    // ── Hydraulics PDF ────────────────────────────────────────────────────────
    public async Task ExportHydraulicsPdfAsync(int projectId, string filePath)
    {
        await using var ctx = _db.CreateContext();
        var project  = await ctx.Projects.Include(p => p.WaterSource).FirstOrDefaultAsync(p => p.Id == projectId)
                       ?? throw new Exception("Project not found");
        var segments = await ctx.PipeSegments.Where(s => s.ProjectId == projectId).ToListAsync();
        var nodes    = await ctx.PipeNodes.Where(n => n.ProjectId == projectId).ToListAsync();

        var hydInput = BuildHydraulicInput(project, nodes, segments, ctx);
        var hydResult = _hydEngine.Solve(hydInput);

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.Content().Column(col =>
                {
                    col.Item().Text($"Hydraulics Report – {project.Name}")
                        .FontSize(16).Bold().FontColor(Colors.Teal.Darken2);
                    col.Item().Text($"Status: {(hydResult.Success ? "OK" : "FAILED – " + hydResult.ErrorMessage)}")
                        .FontSize(10);
                    col.Item().PaddingTop(8).Table(t =>
                    {
                        t.ColumnsDefinition(c =>
                        {
                            c.ConstantColumn(40); c.RelativeColumn(); c.RelativeColumn();
                            c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                            c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn();
                        });
                        static IContainer Hdr(IContainer c) =>
                            c.Background(Colors.Teal.Darken2).Padding(3).DefaultTextStyle(s => s.FontColor(Colors.White).Bold().FontSize(8));
                        t.Header(h =>
                        {
                            foreach (var hdr in new[]{"Seg","From","To","Ø(mm)","L(m)","Q(L/min)","v(m/s)","hf(m)","ΔP(bar)"})
                                h.Cell().Element(Hdr).Text(hdr);
                        });
                        foreach (var seg in hydResult.Segments)
                        {
                            t.Cell().Padding(3).Text(seg.SegmentId.ToString());
                            t.Cell().Padding(3).Text(seg.FromNodeId.ToString());
                            t.Cell().Padding(3).Text(seg.ToNodeId.ToString());
                            t.Cell().Padding(3).AlignRight().Text($"{seg.Diameter_mm:F0}");
                            t.Cell().Padding(3).AlignRight().Text($"{seg.Length_m:F1}");
                            t.Cell().Padding(3).AlignRight().Text($"{seg.Flow_Lmin:F2}");
                            t.Cell().Padding(3).AlignRight().Text($"{seg.Velocity_ms:F2}");
                            t.Cell().Padding(3).AlignRight().Text($"{seg.HeadLoss_m:F3}");
                            t.Cell().Padding(3).AlignRight().Text($"{seg.PressureDrop_bar:F3}");
                        }
                    });
                    if (hydResult.Warnings.Any())
                    {
                        col.Item().PaddingTop(10).Text("Warnings:").Bold();
                        foreach (var w in hydResult.Warnings)
                            col.Item().Text($"• {w}").FontColor(Colors.Orange.Medium);
                    }
                });
            });
        }).GeneratePdf(filePath);
    }

    // ── BOM Excel ─────────────────────────────────────────────────────────────
    public async Task ExportBomExcelAsync(int projectId, string filePath)
    {
        await using var ctx = _db.CreateContext();
        var project  = await ctx.Projects.FirstOrDefaultAsync(p => p.Id == projectId)
                       ?? throw new Exception("Project not found");
        var heads    = await ctx.Heads.Include(h => h.Nozzle).Where(h => h.ProjectId == projectId).ToListAsync();
        var segments = await ctx.PipeSegments.Where(s => s.ProjectId == projectId).ToListAsync();
        var valves   = await ctx.Valves.Where(v => v.ProjectId == projectId).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("BOM");
        ws.Cell(1,1).Value = $"Bill of Materials – {project.Name}";
        ws.Cell(1,1).Style.Font.Bold = true;

        int row = 3;
        ws.Cell(row,1).Value = "NOZZLES / HEADS"; ws.Cell(row,1).Style.Font.Bold = true; row++;
        ws.Cell(row,1).Value = "Brand";  ws.Cell(row,2).Value = "Model";
        ws.Cell(row,3).Value = "Count";  ws.Cell(row,4).Value = "Arc(°)"; row++;

        var headGroups = heads
            .GroupBy(h => new { h.Nozzle?.Brand, h.Nozzle?.Model, h.Nozzle?.Arc_deg })
            .Select(g => new { g.Key.Brand, g.Key.Model, g.Key.Arc_deg, Count = g.Count() });

        foreach (var g in headGroups)
        {
            ws.Cell(row,1).Value = g.Brand ?? "No Nozzle";
            ws.Cell(row,2).Value = g.Model ?? "–";
            ws.Cell(row,3).Value = g.Count;
            ws.Cell(row,4).Value = g.Arc_deg ?? 0;
            row++;
        }

        row++; ws.Cell(row,1).Value = "PIPES"; ws.Cell(row,1).Style.Font.Bold = true; row++;
        ws.Cell(row,1).Value = "Material"; ws.Cell(row,2).Value = "Diameter(mm)";
        ws.Cell(row,3).Value = "Total Length(m)"; row++;

        var pipeGroups = segments
            .GroupBy(s => new { s.Material, s.Diameter_mm })
            .Select(g => new { g.Key.Material, g.Key.Diameter_mm, TotalLen = g.Sum(s => s.Length_m) });

        foreach (var g in pipeGroups)
        {
            ws.Cell(row,1).Value = g.Material.ToString();
            ws.Cell(row,2).Value = g.Diameter_mm;
            ws.Cell(row,3).Value = Math.Round(g.TotalLen, 1);
            row++;
        }

        row++; ws.Cell(row,1).Value = "VALVES"; ws.Cell(row,1).Style.Font.Bold = true; row++;
        ws.Cell(row,1).Value = "Size(mm)"; ws.Cell(row,2).Value = "Count"; row++;
        foreach (var vg in valves.GroupBy(v => v.Size_mm))
        {
            ws.Cell(row,1).Value = vg.Key;
            ws.Cell(row,2).Value = vg.Count();
            row++;
        }

        ws.Columns().AdjustToContents();
        wb.SaveAs(filePath);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<(Project project, List<ZoneCalcResult> results)> GetZoneResultsAsync(int projectId)
    {
        await using var ctx = _db.CreateContext();
        var project = await ctx.Projects
            .Include(p => p.WaterSource)
            .Include(p => p.Zones)
            .Include(p => p.Heads).ThenInclude(h => h.Nozzle)
            .FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new Exception("Project not found");

        var inputs = project.Zones.Select(z => new ZoneCalcInput
        {
            ZoneId         = z.Id,
            ZoneName       = z.Name,
            Method         = (IrrigationCalc.Models.IrrigationMethod)z.Method,
            Area_m2        = z.Area_m2,
            DesignPressure_bar = z.DesignPressure_bar,
            TargetDepth_mm = z.TargetDepth_mm,
            Heads          = project.Heads
                .Where(h => h.ZoneId == z.Id)
                .Select(h => new HeadFlowInput
                {
                    HeadId             = h.Id,
                    Flow_Lmin          = h.Nozzle?.Flow_Lmin ?? 0,
                    NozzlePressure_bar = h.Nozzle?.Pressure_bar ?? z.DesignPressure_bar
                }).ToList()
        }).ToList();

        return (project, _zoneCalc.CalculateAll(inputs));
    }

    private static HydraulicInput BuildHydraulicInput(
        Project project,
        List<PipeNode> nodes,
        List<PipeSegment> segments,
        AppDbContext ctx)
    {
        var src = nodes.FirstOrDefault(n => n.Type == NodeTypeDb.Source) ?? nodes.FirstOrDefault();
        return new HydraulicInput
        {
            SourcePressure_bar = project.WaterSource?.StaticPressure_bar ?? 4.0,
            SourceElevation_m  = project.WaterSource?.Elevation_m ?? 0,
            SourceNodeId       = src?.Id ?? 0,
            Nodes = nodes.Select(n => new HydraulicNode
            {
                NodeId      = n.Id,
                Elevation_m = n.Elevation_m,
                Type        = (IrrigationCalc.Models.NodeType)n.Type
            }).ToList(),
            Segments = segments.Select(s => new HydraulicSegment
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
    }
}
