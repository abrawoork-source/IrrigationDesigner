// File: src/IrrigationApp/Services/DatabaseService.cs
using IrrigationApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IrrigationApp.Services;

/// <summary>
/// Manages EF Core database lifecycle and provides the DbContext factory.
/// </summary>
public class DatabaseService
{
    private static DatabaseService? _instance;
    public static DatabaseService Instance => _instance ??= new DatabaseService();

    private readonly ILogger<DatabaseService> _logger;
    private string _connectionString = "";

    private DatabaseService()
    {
        var loggerFactory = LoggerFactory.Create(b => b.AddProvider(new FileLoggerProvider()));
        _logger = loggerFactory.CreateLogger<DatabaseService>();
    }

    public void Initialize(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
        using var ctx = CreateContext();
        ctx.Database.EnsureCreated();
        _logger.LogInformation("Database initialized at {Path}", dbPath);
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connectionString)
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>Seeds the default nozzle catalogue and a sample project on first run.</summary>
    public async Task SeedDefaultDataAsync()
    {
        await using var ctx = CreateContext();

        // ── Nozzles ──────────────────────────────────────────────────────────
        if (!ctx.Nozzles.Any())
        {
            var nozzles = new List<Nozzle>
            {
                // Hunter Spray
                new() { Brand="Hunter", Method="Spray", Model="Pro-S 15",   Arc_deg=90,  Pressure_bar=2.1, Radius_m=4.6, Flow_Lmin=4.4  },
                new() { Brand="Hunter", Method="Spray", Model="Pro-S 15",   Arc_deg=180, Pressure_bar=2.1, Radius_m=4.6, Flow_Lmin=8.7  },
                new() { Brand="Hunter", Method="Spray", Model="Pro-S 15",   Arc_deg=360, Pressure_bar=2.1, Radius_m=4.6, Flow_Lmin=17.4 },
                new() { Brand="Hunter", Method="Spray", Model="Pro-S 12",   Arc_deg=90,  Pressure_bar=2.1, Radius_m=3.7, Flow_Lmin=2.8  },
                new() { Brand="Hunter", Method="Spray", Model="Pro-S 12",   Arc_deg=180, Pressure_bar=2.1, Radius_m=3.7, Flow_Lmin=5.5  },
                new() { Brand="Hunter", Method="Spray", Model="Pro-S 10",   Arc_deg=90,  Pressure_bar=2.1, Radius_m=3.0, Flow_Lmin=1.8  },
                new() { Brand="Hunter", Method="Spray", Model="Pro-S 10",   Arc_deg=180, Pressure_bar=2.1, Radius_m=3.0, Flow_Lmin=3.7  },
                // Hunter MP Rotator
                new() { Brand="Hunter", Method="MP",    Model="MP1000-90",  Arc_deg=90,  Pressure_bar=2.1, Radius_m=3.0, Flow_Lmin=0.68 },
                new() { Brand="Hunter", Method="MP",    Model="MP2000-90",  Arc_deg=90,  Pressure_bar=2.1, Radius_m=5.0, Flow_Lmin=1.18 },
                new() { Brand="Hunter", Method="MP",    Model="MP3000-90",  Arc_deg=90,  Pressure_bar=2.1, Radius_m=8.2, Flow_Lmin=2.40 },
                new() { Brand="Hunter", Method="MP",    Model="MP3000-180", Arc_deg=180, Pressure_bar=2.1, Radius_m=8.2, Flow_Lmin=4.78 },
                new() { Brand="Hunter", Method="MP",    Model="MP3000-360", Arc_deg=360, Pressure_bar=2.1, Radius_m=8.2, Flow_Lmin=9.57 },
                // Rain Bird Spray
                new() { Brand="Rain Bird", Method="Spray", Model="1800-15",   Arc_deg=90,  Pressure_bar=2.1, Radius_m=4.6, Flow_Lmin=4.3  },
                new() { Brand="Rain Bird", Method="Spray", Model="1800-15",   Arc_deg=180, Pressure_bar=2.1, Radius_m=4.6, Flow_Lmin=8.6  },
                new() { Brand="Rain Bird", Method="Spray", Model="1800-15",   Arc_deg=360, Pressure_bar=2.1, Radius_m=4.6, Flow_Lmin=17.2 },
                new() { Brand="Rain Bird", Method="Spray", Model="1800-12",   Arc_deg=90,  Pressure_bar=2.1, Radius_m=3.7, Flow_Lmin=2.7  },
                new() { Brand="Rain Bird", Method="Spray", Model="1800-10",   Arc_deg=90,  Pressure_bar=2.1, Radius_m=3.0, Flow_Lmin=1.7  },
                // Rain Bird MP (R-VAN)
                new() { Brand="Rain Bird", Method="MP",    Model="R-VAN14-180", Arc_deg=180, Pressure_bar=2.1, Radius_m=4.3, Flow_Lmin=1.50 },
                new() { Brand="Rain Bird", Method="MP",    Model="R-VAN18-180", Arc_deg=180, Pressure_bar=2.1, Radius_m=5.5, Flow_Lmin=2.55 },
                new() { Brand="Rain Bird", Method="MP",    Model="R-VAN24-180", Arc_deg=180, Pressure_bar=2.1, Radius_m=7.3, Flow_Lmin=4.10 },
            };
            ctx.Nozzles.AddRange(nozzles);
        }

        // ── Drip Products ────────────────────────────────────────────────────
        if (!ctx.DripProducts.Any())
        {
            var drip = new List<DripProduct>
            {
                new() { Brand="Hunter", Product="DRIPLINE HDL-06",   EmitterFlow_Lph=0.6, EmitterSpacing_m=0.3, LineSpacing_m=0.45, Pressure_bar=1.0 },
                new() { Brand="Hunter", Product="DRIPLINE HDL-10",   EmitterFlow_Lph=1.0, EmitterSpacing_m=0.3, LineSpacing_m=0.45, Pressure_bar=1.0 },
                new() { Brand="Hunter", Product="DRIPLINE HDL-16",   EmitterFlow_Lph=1.6, EmitterSpacing_m=0.3, LineSpacing_m=0.5,  Pressure_bar=1.0 },
                new() { Brand="Rain Bird", Product="XFS-04-06-100",  EmitterFlow_Lph=0.6, EmitterSpacing_m=0.15, LineSpacing_m=0.3, Pressure_bar=0.7 },
                new() { Brand="Rain Bird", Product="XFS-06-12-100",  EmitterFlow_Lph=1.0, EmitterSpacing_m=0.3,  LineSpacing_m=0.5, Pressure_bar=0.7 },
                new() { Brand="Rain Bird", Product="XFS-09-12-100",  EmitterFlow_Lph=1.6, EmitterSpacing_m=0.3,  LineSpacing_m=0.5, Pressure_bar=0.7 },
            };
            ctx.DripProducts.AddRange(drip);
        }

        await ctx.SaveChangesAsync();
        _logger.LogInformation("Default nozzle and drip data seeded.");
    }

    /// <summary>Creates a sample project for demonstration.</summary>
    public async Task<Project> CreateSampleProjectAsync()
    {
        await using var ctx = CreateContext();

        // Remove existing sample
        var existing = ctx.Projects.FirstOrDefault(p => p.Name == "Sample Villa Garden");
        if (existing != null)
        {
            ctx.Projects.Remove(existing);
            await ctx.SaveChangesAsync();
        }

        var project = new Project
        {
            Name  = "Sample Villa Garden",
            Units = "Metric",
            Notes = "Auto-generated sample project demonstrating all features.",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WaterSource = new WaterSource
            {
                StaticPressure_bar  = 4.5,
                AvailableFlow_Lmin  = 80.0,
                Elevation_m         = 0,
                Notes               = "Municipal supply at meter"
            }
        };

        var z1 = new Zone { Name="Front Lawn",   Method=ZoneMethod.Spray, Area_m2=250, DesignPressure_bar=2.1, TargetDepth_mm=12 };
        var z2 = new Zone { Name="Side Border",  Method=ZoneMethod.MP,    Area_m2=120, DesignPressure_bar=2.1, TargetDepth_mm=10 };
        var z3 = new Zone { Name="Kitchen Garden",Method=ZoneMethod.Drip, Area_m2=80,  DesignPressure_bar=1.0, TargetDepth_mm=6  };
        project.Zones = new List<Zone> { z1, z2, z3 };

        await ctx.Projects.AddAsync(project);
        await ctx.SaveChangesAsync();

        // Add heads referencing first nozzle per zone type
        var sprayNozzle = await ctx.Nozzles.FirstOrDefaultAsync(n => n.Method == "Spray" && n.Arc_deg == 90);
        var mpNozzle    = await ctx.Nozzles.FirstOrDefaultAsync(n => n.Method == "MP"    && n.Arc_deg == 90);

        if (sprayNozzle != null)
        {
            for (int i = 0; i < 6; i++)
                ctx.Heads.Add(new Head { ProjectId=project.Id, ZoneId=z1.Id, NozzleId=sprayNozzle.Id });
        }
        if (mpNozzle != null)
        {
            for (int i = 0; i < 4; i++)
                ctx.Heads.Add(new Head { ProjectId=project.Id, ZoneId=z2.Id, NozzleId=mpNozzle.Id });
        }
        // Drip zone: 3 heads (representative for drip emitter rows)
        for (int i = 0; i < 3; i++)
            ctx.Heads.Add(new Head { ProjectId=project.Id, ZoneId=z3.Id, NozzleId=null });

        // Pipe network: Source(1) → Valve1(2) → Head1(3), Valve2(4) → Head2(5)
        var src  = new PipeNode { ProjectId=project.Id, Elevation_m=0,    Type=NodeTypeDb.Source   };
        var val1 = new PipeNode { ProjectId=project.Id, Elevation_m=0,    Type=NodeTypeDb.Valve    };
        var hd1  = new PipeNode { ProjectId=project.Id, Elevation_m=0,    Type=NodeTypeDb.HeadNode };
        var val2 = new PipeNode { ProjectId=project.Id, Elevation_m=-0.5, Type=NodeTypeDb.Valve    };
        var hd2  = new PipeNode { ProjectId=project.Id, Elevation_m=-0.5, Type=NodeTypeDb.HeadNode };
        ctx.PipeNodes.AddRange(src, val1, hd1, val2, hd2);
        await ctx.SaveChangesAsync();

        ctx.PipeSegments.AddRange(
            new PipeSegment { ProjectId=project.Id, FromNodeId=src.Id,  ToNodeId=val1.Id, Material=PipeMaterialDb.PVC, Diameter_mm=63, Length_m=25, FittingsEquivLength_m=3 },
            new PipeSegment { ProjectId=project.Id, FromNodeId=val1.Id, ToNodeId=hd1.Id,  Material=PipeMaterialDb.PVC, Diameter_mm=32, Length_m=15, FittingsEquivLength_m=2 },
            new PipeSegment { ProjectId=project.Id, FromNodeId=src.Id,  ToNodeId=val2.Id, Material=PipeMaterialDb.PVC, Diameter_mm=63, Length_m=30, FittingsEquivLength_m=3 },
            new PipeSegment { ProjectId=project.Id, FromNodeId=val2.Id, ToNodeId=hd2.Id,  Material=PipeMaterialDb.PVC, Diameter_mm=25, Length_m=20, FittingsEquivLength_m=2 }
        );

        ctx.Valves.AddRange(
            new Valve { ProjectId=project.Id, ZoneId=z1.Id, NodeId=val1.Id, Size_mm=25 },
            new Valve { ProjectId=project.Id, ZoneId=z2.Id, NodeId=val2.Id, Size_mm=25 }
        );

        ctx.ControllerStations.AddRange(
            new ControllerStation { ProjectId=project.Id, ZoneId=z1.Id, StationNumber=1 },
            new ControllerStation { ProjectId=project.Id, ZoneId=z2.Id, StationNumber=2 },
            new ControllerStation { ProjectId=project.Id, ZoneId=z3.Id, StationNumber=3 }
        );

        await ctx.SaveChangesAsync();
        _logger.LogInformation("Sample project created: {Id}", project.Id);
        return project;
    }
}
