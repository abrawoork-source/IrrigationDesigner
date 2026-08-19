// File: src/IrrigationApp/ViewModels/NozzleDatabaseViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.Windows;

namespace IrrigationApp.ViewModels;

public class NozzleDatabaseViewModel : BaseViewModel
{
    private readonly DatabaseService  _db;
    private readonly CsvImportService _csv = new();
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public ObservableCollection<NozzleRowVm> Nozzles { get; } = new();

    private NozzleRowVm? _selected;
    public NozzleRowVm? SelectedNozzle
    {
        get => _selected;
        set => SetProperty(ref _selected, value);
    }

    private string _filterBrand = "";
    public string FilterBrand
    {
        get => _filterBrand;
        set { SetProperty(ref _filterBrand, value); _ = ApplyFilterAsync(); }
    }

    private string _filterMethod = "All";
    public string FilterMethod
    {
        get => _filterMethod;
        set { SetProperty(ref _filterMethod, value); _ = ApplyFilterAsync(); }
    }

    public string[] MethodOptions { get; } = { "All", "Spray", "MP" };

    private string _statusMessage = "";
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    public string L_Title      => _loc.Get("Nav_NozzleDatabase");
    public string L_Add        => _loc.Get("Btn_Add");
    public string L_Delete     => _loc.Get("Btn_Delete");
    public string L_Save       => _loc.Get("Btn_Save");
    public string L_Import     => _loc.Get("Btn_Import");
    public string L_Brand      => _loc.Get("Lbl_Brand");
    public string L_Method     => _loc.Get("Lbl_Method");
    public string L_Model      => _loc.Get("Lbl_Model");
    public string L_Arc        => _loc.Get("Lbl_Arc");
    public string L_Pressure   => _loc.Get("Lbl_Pressure");
    public string L_Radius     => _loc.Get("Lbl_Radius");
    public string L_Flow       => _loc.Get("Lbl_Flow");
    public string L_Precip     => _loc.Get("Lbl_Precip");
    public string L_CatalogRef => _loc.Get("Lbl_CatalogRef");

    public AsyncRelayCommand AddCommand    { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand SaveCommand   { get; }
    public AsyncRelayCommand ImportCommand { get; }

    public NozzleDatabaseViewModel(DatabaseService db)
    {
        _db = db;
        AddCommand    = new AsyncRelayCommand(AddAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync,  () => SelectedNozzle != null);
        SaveCommand   = new AsyncRelayCommand(SaveAsync,    () => SelectedNozzle != null);
        ImportCommand = new AsyncRelayCommand(ImportCsvAsync);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using var ctx = _db.CreateContext();
        var list = await ctx.Nozzles.OrderBy(n => n.Brand).ThenBy(n => n.Model).ToListAsync();
        Nozzles.Clear();
        foreach (var n in list) Nozzles.Add(new NozzleRowVm(n));
    }

    private async Task ApplyFilterAsync()
    {
        await using var ctx = _db.CreateContext();
        var q = ctx.Nozzles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(FilterBrand))
            q = q.Where(n => n.Brand.Contains(FilterBrand));
        if (FilterMethod != "All")
            q = q.Where(n => n.Method == FilterMethod);
        var list = await q.OrderBy(n => n.Brand).ThenBy(n => n.Model).ToListAsync();
        Nozzles.Clear();
        foreach (var n in list) Nozzles.Add(new NozzleRowVm(n));
    }

    private async Task AddAsync()
    {
        await using var ctx = _db.CreateContext();
        var n = new Nozzle { Brand="Hunter", Method="Spray", Model="New Nozzle", Arc_deg=90,
                             Pressure_bar=2.1, Radius_m=3, Flow_Lmin=4 };
        ctx.Nozzles.Add(n);
        await ctx.SaveChangesAsync();
        var vm = new NozzleRowVm(n);
        Nozzles.Insert(0, vm);
        SelectedNozzle = vm;
    }

    private async Task DeleteAsync()
    {
        if (SelectedNozzle == null) return;
        var r = MessageBox.Show(_loc.Get("Msg_DeleteConfirm"), "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        await using var ctx = _db.CreateContext();
        var n = await ctx.Nozzles.FindAsync(SelectedNozzle.Id);
        if (n != null) { ctx.Nozzles.Remove(n); await ctx.SaveChangesAsync(); }
        Nozzles.Remove(SelectedNozzle);
        SelectedNozzle = Nozzles.FirstOrDefault();
    }

    private async Task SaveAsync()
    {
        if (SelectedNozzle == null) return;
        await using var ctx = _db.CreateContext();
        var n = await ctx.Nozzles.FindAsync(SelectedNozzle.Id);
        if (n == null) return;
        n.Brand       = SelectedNozzle.Brand;
        n.Method      = SelectedNozzle.Method;
        n.Model       = SelectedNozzle.Model;
        n.Arc_deg     = SelectedNozzle.Arc_deg;
        n.Pressure_bar= SelectedNozzle.Pressure_bar;
        n.Radius_m    = SelectedNozzle.Radius_m;
        n.Flow_Lmin   = SelectedNozzle.Flow_Lmin;
        n.Precip_mmhr = SelectedNozzle.Precip_mmhr;
        n.CatalogRef  = SelectedNozzle.CatalogRef;
        await ctx.SaveChangesAsync();
        StatusMessage = _loc.Get("Msg_SaveSuccess");
    }

    private async Task ImportCsvAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "CSV files|*.csv|All files|*.*",
            Title  = "Import Nozzle Database CSV"
        };
        if (dlg.ShowDialog() != true) return;

        await using var ctx = _db.CreateContext();
        var result = await _csv.ImportNozzlesAsync(dlg.FileName, ctx);
        StatusMessage = string.Format(_loc.Get("Msg_ImportSuccess"), result.Imported);
        await LoadAsync();
        if (result.Errors.Any())
            MessageBox.Show(string.Join("\n", result.Errors), "Import Warnings",
                MessageBoxButton.OK, MessageBoxImage.Warning);
    }
}

public class NozzleRowVm : BaseViewModel
{
    public int Id { get; }
    private string _brand = ""; public string Brand { get => _brand; set => SetProperty(ref _brand, value); }
    private string _method = "Spray"; public string Method { get => _method; set => SetProperty(ref _method, value); }
    private string _model = ""; public string Model { get => _model; set => SetProperty(ref _model, value); }
    private double _arc; public double Arc_deg { get => _arc; set => SetProperty(ref _arc, value); }
    private double _pressure; public double Pressure_bar { get => _pressure; set => SetProperty(ref _pressure, value); }
    private double _radius; public double Radius_m { get => _radius; set => SetProperty(ref _radius, value); }
    private double _flow; public double Flow_Lmin { get => _flow; set => SetProperty(ref _flow, value); }
    private double? _precip; public double? Precip_mmhr { get => _precip; set => SetProperty(ref _precip, value); }
    private string? _cat; public string? CatalogRef { get => _cat; set => SetProperty(ref _cat, value); }

    public NozzleRowVm(Nozzle n)
    {
        Id           = n.Id;
        Brand        = n.Brand;
        Method       = n.Method;
        Model        = n.Model;
        Arc_deg      = n.Arc_deg;
        Pressure_bar = n.Pressure_bar;
        Radius_m     = n.Radius_m;
        Flow_Lmin    = n.Flow_Lmin;
        Precip_mmhr  = n.Precip_mmhr;
        CatalogRef   = n.CatalogRef;
    }
}
