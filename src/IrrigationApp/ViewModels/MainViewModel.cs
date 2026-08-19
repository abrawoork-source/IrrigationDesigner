// File: src/IrrigationApp/ViewModels/MainViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;

namespace IrrigationApp.ViewModels;

public class MainViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;

    // ── Current page ──────────────────────────────────────────────────────────
    private BaseViewModel? _currentPage;
    public BaseViewModel? CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    private string _currentPageName = "Dashboard";
    public string CurrentPageName
    {
        get => _currentPageName;
        set => SetProperty(ref _currentPageName, value);
    }

    // ── Projects ──────────────────────────────────────────────────────────────
    public ObservableCollection<Project> Projects { get; } = new();

    private Project? _selectedProject;
    public Project? SelectedProject
    {
        get => _selectedProject;
        set
        {
            if (SetProperty(ref _selectedProject, value))
                OnProjectChanged();
        }
    }

    // ── Language ──────────────────────────────────────────────────────────────
    private string _currentLanguage = "en";
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (SetProperty(ref _currentLanguage, value))
            {
                _loc.SetLanguage(value);
                OnPropertyChanged(nameof(IsRtl));
                OnPropertyChanged(nameof(FlowDir));
                RefreshLocalization();
            }
        }
    }

    public bool IsRtl => _loc.IsArabic;
    public FlowDirection FlowDir => _loc.GetFlowDirection();

    // ── Status bar ────────────────────────────────────────────────────────────
    private string _statusMessage = "Ready";
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    // ── Localized nav labels ──────────────────────────────────────────────────
    public string L_AppTitle         => _loc.Get("AppTitle");
    public string L_Dashboard        => _loc.Get("Nav_Dashboard");
    public string L_WaterSource      => _loc.Get("Nav_WaterSource");
    public string L_Zones            => _loc.Get("Nav_Zones");
    public string L_NozzleDatabase   => _loc.Get("Nav_NozzleDatabase");
    public string L_DripDatabase     => _loc.Get("Nav_DripDatabase");
    public string L_Heads            => _loc.Get("Nav_Heads");
    public string L_PipeNetwork      => _loc.Get("Nav_PipeNetwork");
    public string L_Calculations     => _loc.Get("Nav_Calculations");
    public string L_Evaluation       => _loc.Get("Nav_Evaluation");
    public string L_Reports          => _loc.Get("Nav_Reports");
    public string L_NewProject       => _loc.Get("Btn_NewProject");
    public string L_LoadSample       => _loc.Get("Btn_LoadSample");
    public string L_SelectProject    => _loc.Get("Lbl_SelectProject");
    public string L_Language         => _loc.Get("Lbl_Language");

    // ── Commands ──────────────────────────────────────────────────────────────
    public RelayCommand NavigateCommand { get; }
    public AsyncRelayCommand NewProjectCommand   { get; }
    public AsyncRelayCommand LoadSampleCommand   { get; }
    public RelayCommand ToggleLanguageCommand    { get; }

    public MainViewModel(DatabaseService db)
    {
        _db = db;
        NavigateCommand      = new RelayCommand(p => Navigate((string?)p));
        NewProjectCommand    = new AsyncRelayCommand(CreateNewProjectAsync);
        LoadSampleCommand    = new AsyncRelayCommand(LoadSampleAsync);
        ToggleLanguageCommand= new RelayCommand(() =>
            CurrentLanguage = CurrentLanguage == "en" ? "ar" : "en");

        _loc.LanguageChanged += RefreshLocalization;
        _ = LoadProjectsAsync();
    }

    private async Task LoadProjectsAsync()
    {
        await using var ctx = _db.CreateContext();
        var list = await ctx.Projects.OrderByDescending(p => p.UpdatedAt).ToListAsync();
        Projects.Clear();
        foreach (var p in list) Projects.Add(p);
        SelectedProject ??= Projects.FirstOrDefault();
        Navigate("Dashboard");
    }

    private async Task CreateNewProjectAsync()
    {
        var name = Views.InputDialog.Show("Enter project name:", "New Project", "My Irrigation Project");
        if (string.IsNullOrWhiteSpace(name)) return;

        await using var ctx = _db.CreateContext();
        var proj = new Project
        {
            Name      = name,
            Units     = "Metric",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            WaterSource = new WaterSource
            {
                StaticPressure_bar = 4.0,
                AvailableFlow_Lmin = 60.0,
                Elevation_m        = 0
            }
        };
        ctx.Projects.Add(proj);
        await ctx.SaveChangesAsync();
        Projects.Insert(0, proj);
        SelectedProject = proj;
        StatusMessage = _loc.Get("Msg_SaveSuccess");
    }

    private async Task LoadSampleAsync()
    {
        var proj = await _db.CreateSampleProjectAsync();
        await LoadProjectsAsync();
        SelectedProject = Projects.FirstOrDefault(p => p.Id == proj.Id);
        StatusMessage = _loc.Get("Msg_SampleLoaded");
    }

    private void OnProjectChanged()
    {
        // Re-navigate to refresh current page with new project
        Navigate(CurrentPageName);
    }

    public void Navigate(string? pageName)
    {
        if (string.IsNullOrEmpty(pageName)) return;
        CurrentPageName = pageName;

        CurrentPage = pageName switch
        {
            "Dashboard"      => new DashboardViewModel(_db, SelectedProject),
            "WaterSource"    => new WaterSourceViewModel(_db, SelectedProject),
            "Zones"          => new ZonesViewModel(_db, SelectedProject),
            "NozzleDatabase" => new NozzleDatabaseViewModel(_db),
            "DripDatabase"   => new DripDatabaseViewModel(_db),
            "Heads"          => new HeadsViewModel(_db, SelectedProject),
            "PipeNetwork"    => new PipeNetworkViewModel(_db, SelectedProject),
            "Calculations"   => new CalculationsViewModel(_db, SelectedProject),
            "Evaluation"     => new EvaluationViewModel(_db, SelectedProject),
            "Reports"        => new ReportsViewModel(_db, SelectedProject),
            _                => new DashboardViewModel(_db, SelectedProject)
        };
    }

    private void RefreshLocalization()
    {
        OnPropertyChanged(nameof(L_AppTitle));
        OnPropertyChanged(nameof(L_Dashboard));
        OnPropertyChanged(nameof(L_WaterSource));
        OnPropertyChanged(nameof(L_Zones));
        OnPropertyChanged(nameof(L_NozzleDatabase));
        OnPropertyChanged(nameof(L_DripDatabase));
        OnPropertyChanged(nameof(L_Heads));
        OnPropertyChanged(nameof(L_PipeNetwork));
        OnPropertyChanged(nameof(L_Calculations));
        OnPropertyChanged(nameof(L_Evaluation));
        OnPropertyChanged(nameof(L_Reports));
        OnPropertyChanged(nameof(L_NewProject));
        OnPropertyChanged(nameof(L_LoadSample));
        OnPropertyChanged(nameof(L_SelectProject));
        OnPropertyChanged(nameof(L_Language));
        OnPropertyChanged(nameof(IsRtl));
        OnPropertyChanged(nameof(FlowDir));
    }
}
