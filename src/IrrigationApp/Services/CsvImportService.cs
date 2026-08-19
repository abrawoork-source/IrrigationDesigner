// File: src/IrrigationApp/Services/CsvImportService.cs
using CsvHelper;
using CsvHelper.Configuration;
using IrrigationApp.Models;
using System.Globalization;
using System.IO;

namespace IrrigationApp.Services;

/// <summary>
/// Imports nozzle data from CSV files.
/// Expected columns (case-insensitive):
/// Brand, Method, Model, Arc_deg, Pressure_bar, Radius_m, Flow_Lmin, Precip_mmhr (optional), CatalogRef (optional)
/// </summary>
public class CsvImportService
{
    public record ImportResult(int Imported, int Skipped, List<string> Errors);

    public async Task<ImportResult> ImportNozzlesAsync(string filePath, AppDbContext ctx)
    {
        var errors  = new List<string>();
        int imported = 0, skipped = 0;

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HeaderValidated    = null,
            MissingFieldFound  = null,
            PrepareHeaderForMatch = args => args.Header.Trim().ToLower()
        };

        using var reader = new StreamReader(filePath);
        using var csv    = new CsvReader(reader, config);

        await foreach (var record in csv.GetRecordsAsync<NozzleCsvRecord>())
        {
            try
            {
                if (string.IsNullOrWhiteSpace(record.Model))
                { skipped++; continue; }

                var nozzle = new Nozzle
                {
                    Brand      = record.Brand?.Trim()      ?? "Unknown",
                    Method     = record.Method?.Trim()     ?? "Spray",
                    Model      = record.Model.Trim(),
                    Arc_deg    = record.Arc_deg,
                    Pressure_bar = record.Pressure_bar,
                    Radius_m   = record.Radius_m,
                    Flow_Lmin  = record.Flow_Lmin,
                    Precip_mmhr = record.Precip_mmhr == 0 ? null : record.Precip_mmhr,
                    CatalogRef = record.CatalogRef?.Trim()
                };
                ctx.Nozzles.Add(nozzle);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row error: {ex.Message}");
                skipped++;
            }
        }

        await ctx.SaveChangesAsync();
        return new ImportResult(imported, skipped, errors);
    }

    private class NozzleCsvRecord
    {
        public string? Brand      { get; set; }
        public string? Method     { get; set; }
        public string? Model      { get; set; }
        public double  Arc_deg    { get; set; }
        public double  Pressure_bar { get; set; }
        public double  Radius_m   { get; set; }
        public double  Flow_Lmin  { get; set; }
        public double  Precip_mmhr { get; set; }
        public string? CatalogRef { get; set; }
    }
}
