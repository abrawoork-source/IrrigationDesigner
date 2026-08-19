// File: src/IrrigationApp/Models/Entities.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IrrigationApp.Models;

public enum ZoneMethod { Spray, MP, Drip }
public enum PipeMaterialDb { PVC, PE }
public enum NodeTypeDb { Source, Valve, Junction, HeadNode }

// ─────────────────────────────────────────────────────────────────────────────
// Project
// ─────────────────────────────────────────────────────────────────────────────
public class Project
{
    [Key] public int Id { get; set; }
    [Required, MaxLength(200)] public string Name { get; set; } = "";
    [MaxLength(20)] public string Units { get; set; } = "Metric";
    [MaxLength(2000)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public WaterSource? WaterSource { get; set; }
    public ICollection<Zone> Zones { get; set; } = new List<Zone>();
    public ICollection<Head> Heads { get; set; } = new List<Head>();
    public ICollection<PipeNode> PipeNodes { get; set; } = new List<PipeNode>();
    public ICollection<PipeSegment> PipeSegments { get; set; } = new List<PipeSegment>();
    public ICollection<Valve> Valves { get; set; } = new List<Valve>();
    public ICollection<ControllerStation> ControllerStations { get; set; } = new List<ControllerStation>();
}

// ─────────────────────────────────────────────────────────────────────────────
// Water Source
// ─────────────────────────────────────────────────────────────────────────────
public class WaterSource
{
    [Key] public int Id { get; set; }
    public int ProjectId { get; set; }
    public double StaticPressure_bar { get; set; } = 4.0;
    public double AvailableFlow_Lmin { get; set; } = 60.0;
    public double Elevation_m { get; set; } = 0;
    [MaxLength(2000)] public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]
    public Project? Project { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Zone
// ─────────────────────────────────────────────────────────────────────────────
public class Zone
{
    [Key] public int Id { get; set; }
    public int ProjectId { get; set; }
    public int? ParentZoneId { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = "";
    public ZoneMethod Method { get; set; } = ZoneMethod.Spray;
    public double Area_m2 { get; set; } = 100;
    public double DesignPressure_bar { get; set; } = 2.5;
    public double TargetDepth_mm { get; set; } = 10;
    [MaxLength(2000)] public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]  public Project? Project   { get; set; }
    [ForeignKey(nameof(ParentZoneId))] public Zone? ParentZone { get; set; }
    public ICollection<Head> Heads { get; set; } = new List<Head>();
    public ICollection<Valve> Valves { get; set; } = new List<Valve>();
}

// ─────────────────────────────────────────────────────────────────────────────
// Nozzle (spray & MP)
// ─────────────────────────────────────────────────────────────────────────────
public class Nozzle
{
    [Key] public int Id { get; set; }
    [MaxLength(100)] public string Brand { get; set; } = "Hunter";
    [MaxLength(50)]  public string Method { get; set; } = "Spray"; // Spray | MP
    [MaxLength(100)] public string Model { get; set; } = "";
    public double Arc_deg { get; set; } = 90;
    public double Pressure_bar { get; set; } = 2.0;
    public double Radius_m { get; set; } = 3.0;
    public double Flow_Lmin { get; set; } = 4.5;
    public double? Precip_mmhr { get; set; }
    [MaxLength(200)] public string? CatalogRef { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Drip Product
// ─────────────────────────────────────────────────────────────────────────────
public class DripProduct
{
    [Key] public int Id { get; set; }
    [MaxLength(100)] public string Brand { get; set; } = "";
    [MaxLength(100)] public string Product { get; set; } = "";
    public double EmitterFlow_Lph { get; set; } = 1.6;
    public double EmitterSpacing_m { get; set; } = 0.3;
    public double LineSpacing_m { get; set; } = 0.5;
    public double Pressure_bar { get; set; } = 1.0;
    [MaxLength(2000)] public string? Notes { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Head
// ─────────────────────────────────────────────────────────────────────────────
public class Head
{
    [Key] public int Id { get; set; }
    public int ProjectId { get; set; }
    public int ZoneId { get; set; }
    public int? NozzleId { get; set; }
    [MaxLength(200)] public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))] public Project? Project { get; set; }
    [ForeignKey(nameof(ZoneId))]    public Zone?    Zone    { get; set; }
    [ForeignKey(nameof(NozzleId))]  public Nozzle?  Nozzle  { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Pipe Node
// ─────────────────────────────────────────────────────────────────────────────
public class PipeNode
{
    [Key] public int Id { get; set; }
    public int ProjectId { get; set; }
    public double Elevation_m { get; set; } = 0;
    public NodeTypeDb Type { get; set; } = NodeTypeDb.Junction;

    [ForeignKey(nameof(ProjectId))] public Project? Project { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Pipe Segment
// ─────────────────────────────────────────────────────────────────────────────
public class PipeSegment
{
    [Key] public int Id { get; set; }
    public int ProjectId { get; set; }
    public int FromNodeId { get; set; }
    public int ToNodeId { get; set; }
    public PipeMaterialDb Material { get; set; } = PipeMaterialDb.PVC;
    public double Diameter_mm { get; set; } = 50;
    public double Length_m { get; set; } = 10;
    public double FittingsEquivLength_m { get; set; } = 1;
    [MaxLength(2000)] public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))]  public Project?  Project  { get; set; }
    [ForeignKey(nameof(FromNodeId))] public PipeNode? FromNode { get; set; }
    [ForeignKey(nameof(ToNodeId))]   public PipeNode? ToNode   { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Valve
// ─────────────────────────────────────────────────────────────────────────────
public class Valve
{
    [Key] public int Id { get; set; }
    public int ProjectId { get; set; }
    public int ZoneId { get; set; }
    public int? NodeId { get; set; }
    public double Size_mm { get; set; } = 25;
    [MaxLength(200)] public string? Notes { get; set; }

    [ForeignKey(nameof(ProjectId))] public Project?  Project  { get; set; }
    [ForeignKey(nameof(ZoneId))]    public Zone?     Zone     { get; set; }
    [ForeignKey(nameof(NodeId))]    public PipeNode? PipeNode { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// Controller Station
// ─────────────────────────────────────────────────────────────────────────────
public class ControllerStation
{
    [Key] public int Id { get; set; }
    public int ProjectId { get; set; }
    public int ZoneId { get; set; }
    public int StationNumber { get; set; } = 1;

    [ForeignKey(nameof(ProjectId))] public Project? Project { get; set; }
    [ForeignKey(nameof(ZoneId))]    public Zone?    Zone    { get; set; }
}
