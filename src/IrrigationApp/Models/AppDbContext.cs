// File: src/IrrigationApp/Models/AppDbContext.cs
using Microsoft.EntityFrameworkCore;

namespace IrrigationApp.Models;

public class AppDbContext : DbContext
{
    public DbSet<Project>           Projects           { get; set; } = null!;
    public DbSet<WaterSource>       WaterSources       { get; set; } = null!;
    public DbSet<Zone>              Zones              { get; set; } = null!;
    public DbSet<Nozzle>            Nozzles            { get; set; } = null!;
    public DbSet<DripProduct>       DripProducts       { get; set; } = null!;
    public DbSet<Head>              Heads              { get; set; } = null!;
    public DbSet<PipeNode>          PipeNodes          { get; set; } = null!;
    public DbSet<PipeSegment>       PipeSegments       { get; set; } = null!;
    public DbSet<Valve>             Valves             { get; set; } = null!;
    public DbSet<ControllerStation> ControllerStations { get; set; } = null!;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Project
        modelBuilder.Entity<Project>()
            .HasOne(p => p.WaterSource)
            .WithOne(w => w.Project)
            .HasForeignKey<WaterSource>(w => w.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.Zones)
            .WithOne(z => z.Project)
            .HasForeignKey(z => z.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.Heads)
            .WithOne(h => h.Project)
            .HasForeignKey(h => h.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.PipeNodes)
            .WithOne(n => n.Project)
            .HasForeignKey(n => n.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.PipeSegments)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.Valves)
            .WithOne(v => v.Project)
            .HasForeignKey(v => v.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.ControllerStations)
            .WithOne(c => c.Project)
            .HasForeignKey(c => c.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Self-referencing zone (parent/child)
        modelBuilder.Entity<Zone>()
            .HasOne(z => z.ParentZone)
            .WithMany()
            .HasForeignKey(z => z.ParentZoneId)
            .OnDelete(DeleteBehavior.Restrict);

        // PipeSegment → PipeNode (restrict to avoid cascade conflict)
        modelBuilder.Entity<PipeSegment>()
            .HasOne(s => s.FromNode)
            .WithMany()
            .HasForeignKey(s => s.FromNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PipeSegment>()
            .HasOne(s => s.ToNode)
            .WithMany()
            .HasForeignKey(s => s.ToNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Valve → PipeNode optional
        modelBuilder.Entity<Valve>()
            .HasOne(v => v.PipeNode)
            .WithMany()
            .HasForeignKey(v => v.NodeId)
            .OnDelete(DeleteBehavior.SetNull);

        // Head → Nozzle optional
        modelBuilder.Entity<Head>()
            .HasOne(h => h.Nozzle)
            .WithMany()
            .HasForeignKey(h => h.NozzleId)
            .OnDelete(DeleteBehavior.SetNull);

        // Enums stored as strings for readability
        modelBuilder.Entity<Zone>()
            .Property(z => z.Method)
            .HasConversion<string>();

        modelBuilder.Entity<PipeSegment>()
            .Property(s => s.Material)
            .HasConversion<string>();

        modelBuilder.Entity<PipeNode>()
            .Property(n => n.Type)
            .HasConversion<string>();
    }
}
