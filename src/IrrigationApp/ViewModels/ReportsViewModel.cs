// File: src/IrrigationApp/ViewModels/ReportsViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using Microsoft.Win32;
using System.Windows;

namespace IrrigationApp.ViewModels;

public class ReportsViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly ReportService   _reports;
    private readonly ProjectExportService _export = new();
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public Project? Project { get; }

    private string _status = "";
    public string StatusMessage { get => _status; set => SetProperty(ref _status, value); }

    public string L_Title         => _loc.Get("Nav_Reports");
    public string L_ZoneSummary   => _loc.Get("Report_ZoneSummary");
    public string L_HeadSchedule  => _loc.Get("Report_HeadSchedule");
    public string L_Hydraulics    => _loc.Get("Report_Hydraulics");
    public string L_BOM           => _loc.Get("Report_BOM");
    public string L_ExportExcel   => _loc.Get("Btn_ExportExcel");
    public string L_ExportPdf     => _loc.Get("Btn_ExportPdf");
    public string L_ExportJson    => _loc.Get("Btn_ExportJson");
    public string L_ImportJson    => _loc.Get("Btn_ImportJson");

    public AsyncRelayCommand ZoneSummaryExcelCommand   { get; }
    public AsyncRelayCommand ZoneSummaryPdfCommand     { get; }
    public AsyncRelayCommand HydraulicsPdfCommand      { get; }
    public AsyncRelayCommand BomExcelCommand           { get; }
    public AsyncRelayCommand ExportProjectJsonCommand  { get; }
    public AsyncRelayCommand ImportProjectJsonCommand  { get; }

    public ReportsViewModel(DatabaseService db, Project? project)
    {
        _db      = db;
        Project  = project;
        _reports = new ReportService(db);

        ZoneSummaryExcelCommand  = new AsyncRelayCommand(ExportZoneSummaryExcelAsync, () => Project != null);
        ZoneSummaryPdfCommand    = new AsyncRelayCommand(ExportZoneSummaryPdfAsync,   () => Project != null);
        HydraulicsPdfCommand     = new AsyncRelayCommand(ExportHydraulicsPdfAsync,    () => Project != null);
        BomExcelCommand          = new AsyncRelayCommand(ExportBomExcelAsync,          () => Project != null);
        ExportProjectJsonCommand = new AsyncRelayCommand(ExportProjectJsonAsync,       () => Project != null);
        ImportProjectJsonCommand = new AsyncRelayCommand(ImportProjectJsonAsync);
    }

    private async Task ExportZoneSummaryExcelAsync()
    {
        var path = GetSavePath("Excel|*.xlsx", "ZoneSummary.xlsx");
        if (path == null) return;
        await _reports.ExportZoneSummaryExcelAsync(Project!.Id, path);
        StatusMessage = string.Format(_loc.Get("Msg_ExportSuccess"), path);
    }

    private async Task ExportZoneSummaryPdfAsync()
    {
        var path = GetSavePath("PDF|*.pdf", "ZoneSummary.pdf");
        if (path == null) return;
        await _reports.ExportZoneSummaryPdfAsync(Project!.Id, path);
        StatusMessage = string.Format(_loc.Get("Msg_ExportSuccess"), path);
    }

    private async Task ExportHydraulicsPdfAsync()
    {
        var path = GetSavePath("PDF|*.pdf", "Hydraulics.pdf");
        if (path == null) return;
        await _reports.ExportHydraulicsPdfAsync(Project!.Id, path);
        StatusMessage = string.Format(_loc.Get("Msg_ExportSuccess"), path);
    }

    private async Task ExportBomExcelAsync()
    {
        var path = GetSavePath("Excel|*.xlsx", "BOM.xlsx");
        if (path == null) return;
        await _reports.ExportBomExcelAsync(Project!.Id, path);
        StatusMessage = string.Format(_loc.Get("Msg_ExportSuccess"), path);
    }

    private async Task ExportProjectJsonAsync()
    {
        var path = GetSavePath("JSON|*.json", $"{Project!.Name}_v1.json");
        if (path == null) return;
        await using var ctx = _db.CreateContext();
        await _export.ExportAsync(Project.Id, path, ctx);
        StatusMessage = string.Format(_loc.Get("Msg_ExportSuccess"), path);
    }

    private async Task ImportProjectJsonAsync()
    {
        var dlg = new OpenFileDialog { Filter = "JSON|*.json|All|*.*", Title = "Import Project JSON" };
        if (dlg.ShowDialog() != true) return;

        var envelope = _export.ParseExport(dlg.FileName);
        if (envelope == null)
        {
            MessageBox.Show("Could not parse project file.", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        MessageBox.Show(
            $"Project '{envelope.Project.Name}' exported on {envelope.ExportedAt:yyyy-MM-dd}.\n" +
            $"Format version: {envelope.FormatVersion}\n\n" +
            "Full import (re-create project) is not yet implemented in v1.\n" +
            "Use the JSON file for backup/transfer purposes.",
            "Import Preview", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static string? GetSavePath(string filter, string defaultName)
    {
        var dlg = new SaveFileDialog { Filter = filter, FileName = defaultName };
        return dlg.ShowDialog() == true ? dlg.FileName : null;
    }
}
