// File: src/IrrigationApp/ViewModels/PipeNetworkViewModel.cs
using IrrigationApp.Models;
using IrrigationApp.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.Windows;

namespace IrrigationApp.ViewModels;

public class PipeNetworkViewModel : BaseViewModel
{
    private readonly DatabaseService _db;
    private readonly LocalizationService _loc = LocalizationService.Instance;
    public Project? Project { get; }

    // ── Nodes ────────────────────────────────────────────────────────────────
    public ObservableCollection<NodeRowVm> Nodes { get; } = new();
    private NodeRowVm? _selectedNode;
    public NodeRowVm? SelectedNode
    {
        get => _selectedNode;
        set => SetProperty(ref _selectedNode, value);
    }

    // ── Segments ──────────────────────────────────────────────────────────────
    public ObservableCollection<SegmentRowVm> Segments { get; } = new();
    private SegmentRowVm? _selectedSegment;
    public SegmentRowVm? SelectedSegment
    {
        get => _selectedSegment;
        set => SetProperty(ref _selectedSegment, value);
    }

    public string[] NodeTypes  { get; } = { "Source", "Valve", "Junction", "HeadNode" };
    public string[] Materials  { get; } = { "PVC", "PE" };

    private string _status = "";
    public string StatusMessage { get => _status; set => SetProperty(ref _status, value); }

    public string L_Title            => _loc.Get("Nav_PipeNetwork");
    public string L_AddNode          => _loc.Get("Btn_Add") + " Node";
    public string L_DeleteNode       => _loc.Get("Btn_Delete") + " Node";
    public string L_SaveNode         => _loc.Get("Btn_Save") + " Node";
    public string L_AddSegment       => _loc.Get("Btn_Add") + " Segment";
    public string L_DeleteSegment    => _loc.Get("Btn_Delete") + " Segment";
    public string L_SaveSegment      => _loc.Get("Btn_Save") + " Segment";
    public string L_Elevation        => _loc.Get("Lbl_Elevation");
    public string L_NodeType         => _loc.Get("Lbl_NodeType");
    public string L_FromNode         => _loc.Get("Lbl_FromNode");
    public string L_ToNode           => _loc.Get("Lbl_ToNode");
    public string L_Material         => _loc.Get("Lbl_Material");
    public string L_Diameter         => _loc.Get("Lbl_Diameter");
    public string L_Length           => _loc.Get("Lbl_Length");
    public string L_FittingsLength   => _loc.Get("Lbl_FittingsLength");
    public string L_Notes            => _loc.Get("Lbl_Notes");

    public AsyncRelayCommand AddNodeCommand      { get; }
    public AsyncRelayCommand DeleteNodeCommand   { get; }
    public AsyncRelayCommand SaveNodeCommand     { get; }
    public AsyncRelayCommand AddSegmentCommand   { get; }
    public AsyncRelayCommand DeleteSegmentCommand{ get; }
    public AsyncRelayCommand SaveSegmentCommand  { get; }

    public PipeNetworkViewModel(DatabaseService db, Project? project)
    {
        _db     = db;
        Project = project;
        AddNodeCommand       = new AsyncRelayCommand(AddNodeAsync);
        DeleteNodeCommand    = new AsyncRelayCommand(DeleteNodeAsync,    () => SelectedNode != null);
        SaveNodeCommand      = new AsyncRelayCommand(SaveNodeAsync,      () => SelectedNode != null);
        AddSegmentCommand    = new AsyncRelayCommand(AddSegmentAsync);
        DeleteSegmentCommand = new AsyncRelayCommand(DeleteSegmentAsync, () => SelectedSegment != null);
        SaveSegmentCommand   = new AsyncRelayCommand(SaveSegmentAsync,   () => SelectedSegment != null);
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (Project == null) return;
        await using var ctx = _db.CreateContext();

        var nodes = await ctx.PipeNodes.Where(n => n.ProjectId == Project.Id).ToListAsync();
        Nodes.Clear();
        foreach (var n in nodes) Nodes.Add(new NodeRowVm(n));

        var segs = await ctx.PipeSegments.Where(s => s.ProjectId == Project.Id).ToListAsync();
        Segments.Clear();
        foreach (var s in segs) Segments.Add(new SegmentRowVm(s));
    }

    // ── Node CRUD ─────────────────────────────────────────────────────────────
    private async Task AddNodeAsync()
    {
        if (Project == null) return;
        await using var ctx = _db.CreateContext();
        var n = new PipeNode { ProjectId = Project.Id, Type = NodeTypeDb.Junction };
        ctx.PipeNodes.Add(n);
        await ctx.SaveChangesAsync();
        var vm = new NodeRowVm(n);
        Nodes.Add(vm);
        SelectedNode = vm;
    }

    private async Task DeleteNodeAsync()
    {
        if (SelectedNode == null) return;
        if (MessageBox.Show(_loc.Get("Msg_DeleteConfirm"), "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await using var ctx = _db.CreateContext();
        var n = await ctx.PipeNodes.FindAsync(SelectedNode.Id);
        if (n != null) { ctx.PipeNodes.Remove(n); await ctx.SaveChangesAsync(); }
        Nodes.Remove(SelectedNode);
        SelectedNode = Nodes.FirstOrDefault();
        await LoadAsync();
    }

    private async Task SaveNodeAsync()
    {
        if (SelectedNode == null) return;
        await using var ctx = _db.CreateContext();
        var n = await ctx.PipeNodes.FindAsync(SelectedNode.Id);
        if (n == null) return;
        n.Elevation_m = SelectedNode.Elevation_m;
        n.Type        = Enum.Parse<NodeTypeDb>(SelectedNode.Type);
        await ctx.SaveChangesAsync();
        StatusMessage = _loc.Get("Msg_SaveSuccess");
    }

    // ── Segment CRUD ──────────────────────────────────────────────────────────
    private async Task AddSegmentAsync()
    {
        if (Project == null) return;
        await using var ctx = _db.CreateContext();
        var first = await ctx.PipeNodes.FirstOrDefaultAsync(n => n.ProjectId == Project.Id);
        if (first == null) { StatusMessage = "Add pipe nodes first."; return; }
        var s = new PipeSegment { ProjectId = Project.Id, FromNodeId = first.Id, ToNodeId = first.Id,
                                  Material = PipeMaterialDb.PVC, Diameter_mm = 50, Length_m = 10 };
        ctx.PipeSegments.Add(s);
        await ctx.SaveChangesAsync();
        var vm = new SegmentRowVm(s);
        Segments.Add(vm);
        SelectedSegment = vm;
    }

    private async Task DeleteSegmentAsync()
    {
        if (SelectedSegment == null) return;
        if (MessageBox.Show(_loc.Get("Msg_DeleteConfirm"), "Confirm",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        await using var ctx = _db.CreateContext();
        var s = await ctx.PipeSegments.FindAsync(SelectedSegment.Id);
        if (s != null) { ctx.PipeSegments.Remove(s); await ctx.SaveChangesAsync(); }
        Segments.Remove(SelectedSegment);
        SelectedSegment = Segments.FirstOrDefault();
    }

    private async Task SaveSegmentAsync()
    {
        if (SelectedSegment == null) return;
        await using var ctx = _db.CreateContext();
        var s = await ctx.PipeSegments.FindAsync(SelectedSegment.Id);
        if (s == null) return;
        s.FromNodeId          = SelectedSegment.FromNodeId;
        s.ToNodeId            = SelectedSegment.ToNodeId;
        s.Material            = Enum.Parse<PipeMaterialDb>(SelectedSegment.Material);
        s.Diameter_mm         = SelectedSegment.Diameter_mm;
        s.Length_m            = SelectedSegment.Length_m;
        s.FittingsEquivLength_m = SelectedSegment.FittingsEquivLength_m;
        s.Notes               = SelectedSegment.Notes;
        await ctx.SaveChangesAsync();
        StatusMessage = _loc.Get("Msg_SaveSuccess");
    }
}

public class NodeRowVm : BaseViewModel
{
    public int Id { get; }
    private double _elev; public double Elevation_m { get => _elev; set => SetProperty(ref _elev, value); }
    private string _type = "Junction"; public string Type { get => _type; set => SetProperty(ref _type, value); }
    public string DisplayName => $"Node {Id} ({Type})";

    public NodeRowVm(PipeNode n)
    {
        Id          = n.Id;
        Elevation_m = n.Elevation_m;
        Type        = n.Type.ToString();
    }
}

public class SegmentRowVm : BaseViewModel
{
    public int Id { get; }
    private int _from; public int FromNodeId { get => _from; set => SetProperty(ref _from, value); }
    private int _to;   public int ToNodeId   { get => _to;   set => SetProperty(ref _to, value); }
    private string _mat = "PVC"; public string Material { get => _mat; set => SetProperty(ref _mat, value); }
    private double _diam; public double Diameter_mm { get => _diam; set => SetProperty(ref _diam, value); }
    private double _len;  public double Length_m { get => _len; set => SetProperty(ref _len, value); }
    private double _fit;  public double FittingsEquivLength_m { get => _fit; set => SetProperty(ref _fit, value); }
    private string? _notes; public string? Notes { get => _notes; set => SetProperty(ref _notes, value); }

    public SegmentRowVm(PipeSegment s)
    {
        Id                    = s.Id;
        FromNodeId            = s.FromNodeId;
        ToNodeId              = s.ToNodeId;
        Material              = s.Material.ToString();
        Diameter_mm           = s.Diameter_mm;
        Length_m              = s.Length_m;
        FittingsEquivLength_m = s.FittingsEquivLength_m;
        Notes                 = s.Notes;
    }
}
