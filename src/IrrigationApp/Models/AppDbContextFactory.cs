// File: src/IrrigationApp/Models/AppDbContextFactory.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace IrrigationApp.Models;

/// <summary>
/// Used by EF Core Tools (dotnet ef migrations) at design time.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var folder = AppDomain.CurrentDomain.BaseDirectory;
        var dbPath = Path.Combine(folder, "irrigation.db");

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new AppDbContext(options);
    }
}
