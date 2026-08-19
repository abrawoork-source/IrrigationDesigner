// File: src/IrrigationApp/ViewModels/ZonesViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;

namespace IrrigationApp.ViewModels;

public class ZonesViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public Project? Project { get; }

    public ObservableCollection<ZoneRowVm> Zones { get; } = new();

    private ZoneRowVm? _selectedZone;
    public ZoneRowVm? SelectedZone
    {
        get => _selectedZone;
        set => SetProperty(ref _selectedZone, value);
    }

    private string _statusMessage = "";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string[] Methods { get; } = { "Spray", "MP", "Drip" };

    // ── Localization ──────────────────────────────────────────────────────────
    public string L_Title          => _loc.Get("Nav_Zones");
    public string L_Add            => _loc.Get("Btn_Add");
    public string L_Delete         => _loc.Get("Btn_Delete");
    public string L_Save           => _loc.Get("Btn_Save");
    public string L_ZoneName       => _loc.Get("Lbl_ZoneName");
    public string L_Method         => _loc.Get("Lbl_Method");
    public string L_Area           => _loc.Get("Lbl_Area");
    public string L_DesignPressure => _loc.Get("Lbl_DesignPressure");
    public string L_TargetDepth    => _loc.Get("Lbl_TargetDepth");
    public string L_Notes          => _loc.Get("Lbl_Notes");

    public AsyncRelayCommand AddCommand    { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand SaveCommand   { get; }

    public ZonesViewModel(DatabaseService db, Project? project)
    {
        _db     = db;
        Project = project;
        AddCommand    = new AsyncRelayCommand(AddZoneAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteZoneAsync, () => SelectedZone != null);
        SaveCommand   = new AsyncRelayCommand(SaveZoneAsync,   () => SelectedZone != null);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Project == null) return;
        await using var ctx = _db.CreateContext();
        var zones = await ctx.Zones.Where(z => z.ProjectId == Project.Id).ToListAsync();
        Zones.Clear();
        foreach (var z in zones) Zones.Add(new ZoneRowVm(z));
    }

    private async Task AddZoneAsync()
    {
        if (Project == null) { StatusMessage = _loc.Get("Msg_NoProject"); return; }
        await using var ctx = _db.CreateContext();
        var zone = new Zone
        {
            ProjectId = Project.Id, Name = "New Zone",
            Method = ZoneMethod.Spray, Area_m2 = 100,
            DesignPressure_bar = 2.1, TargetDepth_mm = 10
        };
        ctx.Zones.Add(zone);
        await ctx.SaveChangesAsync();
        var vm = new ZoneRowVm(zone);
        Zones.Add(vm);
        SelectedZone = vm;
    }

    private async Task DeleteZoneAsync()
    {
        if (SelectedZone == null) return;
        var r = MessageBox.Show(_loc.Get("Msg_DeleteConfirm"), "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        await using var ctx = _db.CreateContext();
        var zone = await ctx.Zones.FindAsync(SelectedZone.Id);
        if (zone != null) { ctx.Zones.Remove(zone); await ctx.SaveChangesAsync(); }
        Zones.Remove(SelectedZone);
        SelectedZone = Zones.FirstOrDefault();
    }

    private async Task SaveZoneAsync()
    {
        if (SelectedZone == null) return;
        await using var ctx = _db.CreateContext();
        var zone = await ctx.Zones.FindAsync(SelectedZone.Id);
        if (zone == null) return;
        zone.Name               = SelectedZone.Name;
        zone.Method             = Enum.Parse<ZoneMethod>(SelectedZone.Method);
        zone.Area_m2            = SelectedZone.Area_m2;
        zone.DesignPressure_bar = SelectedZone.DesignPressure_bar;
        zone.TargetDepth_mm     = SelectedZone.TargetDepth_mm;
        zone.Notes              = SelectedZone.Notes;
        await ctx.SaveChangesAsync();
        StatusMessage = _loc.Get("Msg_SaveSuccess");
    }
}

public class ZoneRowVm : BaseViewModel
{
    public int Id { get; }

    private string _name = "";
    public string Name { get => _name; set => SetProperty(ref _name, value); }

    private string _method = "Spray";
    public string Method { get => _method; set => SetProperty(ref _method, value); }

    private double _area;
    public double Area_m2 { get => _area; set => SetProperty(ref _area, value); }

    private double _designPressure;
    public double DesignPressure_bar { get => _designPressure; set => SetProperty(ref _designPressure, value); }

    private double _targetDepth;
    public double TargetDepth_mm { get => _targetDepth; set => SetProperty(ref _targetDepth, value); }

    private string? _notes;
    public string? Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public ZoneRowVm(Zone z)
    {
        Id                 = z.Id;
        Name               = z.Name;
        Method             = z.Method.ToString();
        Area_m2            = z.Area_m2;
        DesignPressure_bar = z.DesignPressure_bar;
        TargetDepth_mm     = z.TargetDepth_mm;
        Notes              = z.Notes;
    }
}
