// File: src/IrrigationApp/ViewModels/WaterSourceViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using Microsoft.EntityFrameworkCore;

namespace IrrigationApp.ViewModels;

public class WaterSourceViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public Project? Project { get; }

    private WaterSource? _source;

    private double _staticPressure;
    public double StaticPressure_bar
    {
        get => _staticPressure;
        set => SetProperty(ref _staticPressure, value);
    }

    private double _availableFlow;
    public double AvailableFlow_Lmin
    {
        get => _availableFlow;
        set => SetProperty(ref _availableFlow, value);
    }

    private double _elevation;
    public double Elevation_m
    {
        get => _elevation;
        set => SetProperty(ref _elevation, value);
    }

    private string? _notes;
    public string? Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // ── Derived display ───────────────────────────────────────────────────────
    public double Flow_m3h => Math.Round(IrrigationCalc.Calculations.UnitConverter.LminToM3h(AvailableFlow_Lmin), 3);
    public double Pressure_kpa => Math.Round(IrrigationCalc.Calculations.UnitConverter.BarToKpa(StaticPressure_bar), 1);

    // ── Localization ──────────────────────────────────────────────────────────
    public string L_Title          => _loc.Get("Nav_WaterSource");
    public string L_StaticPressure => _loc.Get("Lbl_StaticPressure");
    public string L_Flow           => _loc.Get("Lbl_AvailableFlow");
    public string L_Elevation      => _loc.Get("Lbl_Elevation");
    public string L_Notes          => _loc.Get("Lbl_Notes");
    public string L_Save           => _loc.Get("Btn_Save");

    public AsyncRelayCommand SaveCommand { get; }

    public WaterSourceViewModel(DatabaseService db, Project? project)
    {
        _db     = db;
        Project = project;
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Project == null) return;
        await using var ctx = _db.CreateContext();
        _source = await ctx.WaterSources.FirstOrDefaultAsync(w => w.ProjectId == Project.Id);
        if (_source == null)
        {
            _source = new WaterSource { ProjectId = Project.Id };
        }
        StaticPressure_bar  = _source.StaticPressure_bar;
        AvailableFlow_Lmin  = _source.AvailableFlow_Lmin;
        Elevation_m         = _source.Elevation_m;
        Notes               = _source.Notes;
        OnPropertyChanged(nameof(Flow_m3h));
        OnPropertyChanged(nameof(Pressure_kpa));
    }

    private async Task SaveAsync()
    {
        if (Project == null) { StatusMessage = _loc.Get("Msg_NoProject"); return; }
        await using var ctx = _db.CreateContext();
        var source = await ctx.WaterSources.FirstOrDefaultAsync(w => w.ProjectId == Project.Id);
        if (source == null)
        {
            source = new WaterSource { ProjectId = Project.Id };
            ctx.WaterSources.Add(source);
        }
        source.StaticPressure_bar  = StaticPressure_bar;
        source.AvailableFlow_Lmin  = AvailableFlow_Lmin;
        source.Elevation_m         = Elevation_m;
        source.Notes               = Notes;
        await ctx.SaveChangesAsync();
        StatusMessage = _loc.Get("Msg_SaveSuccess");
        OnPropertyChanged(nameof(Flow_m3h));
        OnPropertyChanged(nameof(Pressure_kpa));
    }
}
