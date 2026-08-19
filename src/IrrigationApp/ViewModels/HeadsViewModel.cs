// File: src/IrrigationApp/ViewModels/HeadsViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;

namespace IrrigationApp.ViewModels;

public class HeadsViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public Project? Project { get; }

    public ObservableCollection<HeadRowVm> Heads { get; } = new();
    public ObservableCollection<Zone>      Zones   { get; } = new();
    public ObservableCollection<Nozzle>    Nozzles { get; } = new();

    private HeadRowVm? _selected;
    public HeadRowVm? SelectedHead { get => _selected; set => SetProperty(ref _selected, value); }

    private string _status = "";
    public string StatusMessage { get => _status; set => SetProperty(ref _status, value); }

    public string L_Title  => _loc.Get("Nav_Heads");
    public string L_Add    => _loc.Get("Btn_Add");
    public string L_Delete => _loc.Get("Btn_Delete");
    public string L_Save   => _loc.Get("Btn_Save");
    public string L_Zone   => _loc.Get("Lbl_Zone");
    public string L_Nozzle => _loc.Get("Lbl_Nozzle");
    public string L_Flow   => _loc.Get("Lbl_Flow");
    public string L_Notes  => _loc.Get("Lbl_Notes");

    public AsyncRelayCommand AddCommand    { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand SaveCommand   { get; }

    public HeadsViewModel(DatabaseService db, Project? project)
    {
        _db     = db;
        Project = project;
        AddCommand    = new AsyncRelayCommand(AddAsync,    () => Project != null);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedHead != null);
        SaveCommand   = new AsyncRelayCommand(SaveAsync,   () => SelectedHead != null);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Project == null) return;
        await using var ctx = _db.CreateContext();

        var zones = await ctx.Zones.Where(z => z.ProjectId == Project.Id).ToListAsync();
        Zones.Clear();
        foreach (var z in zones) Zones.Add(z);

        var nozzles = await ctx.Nozzles.OrderBy(n => n.Brand).ThenBy(n => n.Model).ToListAsync();
        Nozzles.Clear();
        Nozzles.Add(new Nozzle { Id = 0, Brand = "–", Model = "No Nozzle" });
        foreach (var n in nozzles) Nozzles.Add(n);

        var heads = await ctx.Heads
            .Include(h => h.Zone)
            .Include(h => h.Nozzle)
            .Where(h => h.ProjectId == Project.Id)
            .ToListAsync();

        Heads.Clear();
        foreach (var h in heads) Heads.Add(new HeadRowVm(h));
    }

    private async Task AddAsync()
    {
        if (Project == null) return;
        await using var ctx = _db.CreateContext();
        var firstZone = await ctx.Zones.FirstOrDefaultAsync(z => z.ProjectId == Project.Id);
        if (firstZone == null) { StatusMessage = "Add a zone first."; return; }
        var head = new Head { ProjectId = Project.Id, ZoneId = firstZone.Id };
        ctx.Heads.Add(head);
        await ctx.SaveChangesAsync();
        await LoadAsync();
        SelectedHead = Heads.LastOrDefault();
    }

    private async Task DeleteAsync()
    {
        if (SelectedHead == null) return;
        if (MessageBox.Show(_loc.Get("Msg_DeleteConfirm"), "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await using var ctx = _db.CreateContext();
        var h = await ctx.Heads.FindAsync(SelectedHead.Id);
        if (h != null) { ctx.Heads.Remove(h); await ctx.SaveChangesAsync(); }
        Heads.Remove(SelectedHead);
        SelectedHead = Heads.FirstOrDefault();
    }

    private async Task SaveAsync()
    {
        if (SelectedHead == null) return;
        await using var ctx = _db.CreateContext();
        var h = await ctx.Heads.FindAsync(SelectedHead.Id);
        if (h == null) return;
        h.ZoneId   = SelectedHead.ZoneId;
        h.NozzleId = SelectedHead.NozzleId == 0 ? null : SelectedHead.NozzleId;
        h.Notes    = SelectedHead.Notes;
        await ctx.SaveChangesAsync();
        StatusMessage = _loc.Get("Msg_SaveSuccess");
        await LoadAsync();
    }
}

public class HeadRowVm : BaseViewModel
{
    public int Id { get; }
    private int _zoneId; public int ZoneId { get => _zoneId; set => SetProperty(ref _zoneId, value); }
    private string _zoneName = ""; public string ZoneName { get => _zoneName; set => SetProperty(ref _zoneName, value); }
    private int? _nozzleId; public int? NozzleId { get => _nozzleId; set => SetProperty(ref _nozzleId, value); }
    private string _nozzleName = ""; public string NozzleName { get => _nozzleName; set => SetProperty(ref _nozzleName, value); }
    private double _flow; public double Flow_Lmin { get => _flow; set => SetProperty(ref _flow, value); }
    private string? _notes; public string? Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public HeadRowVm(Head h)
    {
        Id         = h.Id;
        ZoneId     = h.ZoneId;
        ZoneName   = h.Zone?.Name ?? "";
        NozzleId   = h.NozzleId ?? 0;
        NozzleName = h.Nozzle != null ? $"{h.Nozzle.Brand} {h.Nozzle.Model}" : "–";
        Flow_Lmin  = h.Nozzle?.Flow_Lmin ?? 0;
        Notes      = h.Notes;
    }
}
