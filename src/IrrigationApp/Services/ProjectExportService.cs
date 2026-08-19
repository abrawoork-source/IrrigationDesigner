// File: src/IrrigationApp/Services/ProjectExportService.cs
using IrrigationApp.Models;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace IrrigationApp.Services;

/// <summary>
/// Versioned project export/import as JSON.
/// </summary>
public class ProjectExportService
{
    private const int FormatVersion = 1;

    public record ExportEnvelope(
        int FormatVersion,
        DateTime ExportedAt,
        string AppVersion,
        ProjectExportDto Project);

    public record ProjectExportDto(
        string Name, string Units, string? Notes,
        DateTime CreatedAt, DateTime UpdatedAt,
        WaterSourceDto? WaterSource,
        List<ZoneDto> Zones,
        List<NozzleDto> Nozzles,
        List<DripProductDto> DripProducts,
        List<HeadDto> Heads,
        List<PipeNodeDto> PipeNodes,
        List<PipeSegmentDto> PipeSegments,
        List<ValveDto> Valves,
        List<ControllerStationDto> ControllerStations);

    public record WaterSourceDto(double StaticPressure_bar, double AvailableFlow_Lmin, double Elevation_m, string? Notes);
    public record ZoneDto(int Id, string Name, string Method, double Area_m2, double DesignPressure_bar, double TargetDepth_mm, int? ParentZoneId, string? Notes);
    public record NozzleDto(string Brand, string Method, string Model, double Arc_deg, double Pressure_bar, double Radius_m, double Flow_Lmin, double? Precip_mmhr, string? CatalogRef);
    public record DripProductDto(string Brand, string Product, double EmitterFlow_Lph, double EmitterSpacing_m, double LineSpacing_m, double Pressure_bar, string? Notes);
    public record HeadDto(int ZoneId, int? NozzleLocalId, string? Notes);
    public record PipeNodeDto(int Id, double Elevation_m, string Type);
    public record PipeSegmentDto(int FromNodeId, int ToNodeId, string Material, double Diameter_mm, double Length_m, double FittingsEquivLength_m, string? Notes);
    public record ValveDto(int ZoneId, int? NodeId, double Size_mm, string? Notes);
    public record ControllerStationDto(int ZoneId, int StationNumber);

    private static readonly JsonSerializerSettings _settings = new()
    {
        Formatting          = Formatting.Indented,
        NullValueHandling   = NullValueHandling.Ignore,
        Converters          = { new StringEnumConverter() }
    };

    public async Task ExportAsync(int projectId, string filePath, AppDbContext ctx)
    {
        var project = await ctx.Projects
            .Include(p => p.WaterSource)
            .Include(p => p.Zones)
            .Include(p => p.Heads).ThenInclude(h => h.Nozzle)
            .Include(p => p.PipeNodes)
            .Include(p => p.PipeSegments)
            .Include(p => p.Valves)
            .Include(p => p.ControllerStations)
            .FirstOrDefaultAsync(p => p.Id == projectId)
            ?? throw new InvalidOperationException($"Project {projectId} not found.");

        var nozzles     = await ctx.Nozzles.ToListAsync();
        var dripProducts= await ctx.DripProducts.ToListAsync();

        // Build a local nozzle ID map for portability (use index)
        var nozzleMap = nozzles.Select((n, i) => (n, i)).ToDictionary(x => x.n.Id, x => x.i);

        var dto = new ProjectExportDto(
            project.Name, project.Units, project.Notes,
            project.CreatedAt, project.UpdatedAt,
            project.WaterSource == null ? null : new WaterSourceDto(
                project.WaterSource.StaticPressure_bar,
                project.WaterSource.AvailableFlow_Lmin,
                project.WaterSource.Elevation_m,
                project.WaterSource.Notes),
            project.Zones.Select(z => new ZoneDto(
                z.Id, z.Name, z.Method.ToString(), z.Area_m2,
                z.DesignPressure_bar, z.TargetDepth_mm, z.ParentZoneId, z.Notes)).ToList(),
            nozzles.Select(n => new NozzleDto(
                n.Brand, n.Method, n.Model, n.Arc_deg,
                n.Pressure_bar, n.Radius_m, n.Flow_Lmin, n.Precip_mmhr, n.CatalogRef)).ToList(),
            dripProducts.Select(d => new DripProductDto(
                d.Brand, d.Product, d.EmitterFlow_Lph,
                d.EmitterSpacing_m, d.LineSpacing_m, d.Pressure_bar, d.Notes)).ToList(),
            project.Heads.Select(h => new HeadDto(
                h.ZoneId,
                h.NozzleId.HasValue && nozzleMap.TryGetValue(h.NozzleId.Value, out var ni) ? ni : null,
                h.Notes)).ToList(),
            project.PipeNodes.Select(n => new PipeNodeDto(n.Id, n.Elevation_m, n.Type.ToString())).ToList(),
            project.PipeSegments.Select(s => new PipeSegmentDto(
                s.FromNodeId, s.ToNodeId, s.Material.ToString(),
                s.Diameter_mm, s.Length_m, s.FittingsEquivLength_m, s.Notes)).ToList(),
            project.Valves.Select(v => new ValveDto(
                v.ZoneId, v.NodeId, v.Size_mm, v.Notes)).ToList(),
            project.ControllerStations.Select(c => new ControllerStationDto(
                c.ZoneId, c.StationNumber)).ToList()
        );

        var envelope = new ExportEnvelope(FormatVersion, DateTime.UtcNow,
            typeof(ProjectExportService).Assembly.GetName().Version?.ToString() ?? "1.0.0", dto);

        var json = JsonConvert.SerializeObject(envelope, _settings);
        await File.WriteAllTextAsync(filePath, json);
    }

    public ExportEnvelope? ParseExport(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return JsonConvert.DeserializeObject<ExportEnvelope>(json, _settings);
    }
}
