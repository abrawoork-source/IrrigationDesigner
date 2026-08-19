// File: src/IrrigationApp/ViewModels/DripDatabaseViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;

namespace IrrigationApp.ViewModels;

public class DripDatabaseViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;

    public ObservableCollection<DripRowVm> Products { get; } = new();

    private DripRowVm? _selected;
    public DripRowVm? SelectedProduct { get => _selected; set => SetProperty(ref _selected, value); }

    private string _status = "";
    public string StatusMessage { get => _status; set => SetProperty(ref _status, value); }

    public string L_Title          => _loc.Get("Nav_DripDatabase");
    public string L_Add            => _loc.Get("Btn_Add");
    public string L_Delete         => _loc.Get("Btn_Delete");
    public string L_Save           => _loc.Get("Btn_Save");
    public string L_Brand          => _loc.Get("Lbl_Brand");
    public string L_Product        => _loc.Get("Lbl_Product");
    public string L_EmitterFlow    => _loc.Get("Lbl_EmitterFlow");
    public string L_EmitterSpacing => _loc.Get("Lbl_EmitterSpacing");
    public string L_LineSpacing    => _loc.Get("Lbl_LineSpacing");
    public string L_Pressure       => _loc.Get("Lbl_Pressure");
    public string L_Notes          => _loc.Get("Lbl_Notes");

    public AsyncRelayCommand AddCommand    { get; }
    public AsyncRelayCommand DeleteCommand { get; }
    public AsyncRelayCommand SaveCommand   { get; }

    public DripDatabaseViewModel(DatabaseService db)
    {
        _db = db;
        AddCommand    = new AsyncRelayCommand(AddAsync);
        DeleteCommand = new AsyncRelayCommand(DeleteAsync, () => SelectedProduct != null);
        SaveCommand   = new AsyncRelayCommand(SaveAsync,   () => SelectedProduct != null);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        await using var ctx = _db.CreateContext();
        var list = await ctx.DripProducts.OrderBy(d => d.Brand).ThenBy(d => d.Product).ToListAsync();
        Products.Clear();
        foreach (var d in list) Products.Add(new DripRowVm(d));
    }

    private async Task AddAsync()
    {
        await using var ctx = _db.CreateContext();
        var d = new DripProduct { Brand="Hunter", Product="New Drip", EmitterFlow_Lph=1.6,
                                  EmitterSpacing_m=0.3, LineSpacing_m=0.5, Pressure_bar=1.0 };
        ctx.DripProducts.Add(d);
        await ctx.SaveChangesAsync();
        var vm = new DripRowVm(d);
        Products.Insert(0, vm);
        SelectedProduct = vm;
    }

    private async Task DeleteAsync()
    {
        if (SelectedProduct == null) return;
        if (MessageBox.Show(_loc.Get("Msg_DeleteConfirm"), "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await using var ctx = _db.CreateContext();
        var d = await ctx.DripProducts.FindAsync(SelectedProduct.Id);
        if (d != null) { ctx.DripProducts.Remove(d); await ctx.SaveChangesAsync(); }
        Products.Remove(SelectedProduct);
        SelectedProduct = Products.FirstOrDefault();
    }

    private async Task SaveAsync()
    {
        if (SelectedProduct == null) return;
        await using var ctx = _db.CreateContext();
        var d = await ctx.DripProducts.FindAsync(SelectedProduct.Id);
        if (d == null) return;
        d.Brand           = SelectedProduct.Brand;
        d.Product         = SelectedProduct.Product;
        d.EmitterFlow_Lph = SelectedProduct.EmitterFlow_Lph;
        d.EmitterSpacing_m= SelectedProduct.EmitterSpacing_m;
        d.LineSpacing_m   = SelectedProduct.LineSpacing_m;
        d.Pressure_bar    = SelectedProduct.Pressure_bar;
        d.Notes           = SelectedProduct.Notes;
        await ctx.SaveChangesAsync();
        StatusMessage = _loc.Get("Msg_SaveSuccess");
    }
}

public class DripRowVm : BaseViewModel
{
    public int Id { get; }
    private string _brand = ""; public string Brand { get => _brand; set => SetProperty(ref _brand, value); }
    private string _product = ""; public string Product { get => _product; set => SetProperty(ref _product, value); }
    private double _flow; public double EmitterFlow_Lph { get => _flow; set => SetProperty(ref _flow, value); }
    private double _eSpacing; public double EmitterSpacing_m { get => _eSpacing; set => SetProperty(ref _eSpacing, value); }
    private double _lSpacing; public double LineSpacing_m { get => _lSpacing; set => SetProperty(ref _lSpacing, value); }
    private double _pressure; public double Pressure_bar { get => _pressure; set => SetProperty(ref _pressure, value); }
    private string? _notes; public string? Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public DripRowVm(DripProduct d)
    {
        Id               = d.Id;
        Brand            = d.Brand;
        Product          = d.Product;
        EmitterFlow_Lph  = d.EmitterFlow_Lph;
        EmitterSpacing_m = d.EmitterSpacing_m;
        LineSpacing_m    = d.LineSpacing_m;
        Pressure_bar     = d.Pressure_bar;
        Notes            = d.Notes;
    }
}
