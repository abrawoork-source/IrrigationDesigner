# Irrigation Design Pro

A professional irrigation network design application for Windows built with **WPF + .NET 8**, supporting **Arabic/English** bilingual interface.

---

## Prerequisites

| Requirement | Details |
|---|---|
| OS | Windows 10/11 (64-bit) |
| Runtime | .NET 8 SDK ([download](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)) |
| IDE | Visual Studio 2022 (17.8+) with **".NET desktop development"** workload |

---

## Build & Run

### Option A – Visual Studio 2022
1. Open `IrrigationDesigner.sln` in Visual Studio 2022
2. Restore NuGet packages (auto on first build)
3. Set **IrrigationApp** as Startup Project
4. Press **F5** (Debug) or **Ctrl+F5** (Release)

### Option B – Command Line (Developer Command Prompt / PowerShell)
```bash
cd IrrigationDesigner

# Restore all packages
dotnet restore

# Build entire solution
dotnet build

# Run unit tests
dotnet test tests/IrrigationCalc.Tests/IrrigationCalc.Tests.csproj --verbosity normal

# Run the WPF application (Windows only)
dotnet run --project src/IrrigationApp/IrrigationApp.csproj
```

### EF Core Migrations (if you need to re-create the schema)
```bash
cd src/IrrigationApp
dotnet ef migrations add InitialCreate --output-dir Migrations
dotnet ef database update
```
> The app uses `EnsureCreated()` on startup, so migrations are optional for the first run.

---

## First Run
- On first launch the app automatically **seeds the nozzle database** (Hunter + Rain Bird preloaded).
- Click **"Load Sample Project"** in the top bar to create a demo project with zones, heads, pipe network, valves, and controller stations.

---

## Application Structure

```
IrrigationDesigner/
├── IrrigationDesigner.sln
├── global.json
├── src/
│   ├── IrrigationCalc/                 # Calculation engine (class library, no UI deps)
│   │   ├── Models/DomainModels.cs      # Input/output DTOs
│   │   ├── Calculations/
│   │   │   ├── UnitConverter.cs        # Unit conversions
│   │   │   ├── ZoneCalculator.cs       # PR, runtime, total flow
│   │   │   └── HydraulicsEngine.cs     # Hazen-Williams, tree solve, loop detect
│   │   └── Validation/EvaluationEngine.cs  # Design checker / rating
│   │
│   └── IrrigationApp/                  # WPF application
│       ├── Models/
│       │   ├── Entities.cs             # EF Core entity classes
│       │   ├── AppDbContext.cs         # EF Core DbContext
│       │   └── AppDbContextFactory.cs  # Design-time factory for migrations
│       ├── ViewModels/                 # MVVM ViewModels
│       │   ├── BaseViewModel.cs        # INotifyPropertyChanged, RelayCommand
│       │   ├── MainViewModel.cs        # Shell / navigation
│       │   ├── DashboardViewModel.cs   # Project overview + design check
│       │   ├── WaterSourceViewModel.cs
│       │   ├── ZonesViewModel.cs
│       │   ├── NozzleDatabaseViewModel.cs
│       │   ├── DripDatabaseViewModel.cs
│       │   ├── HeadsViewModel.cs
│       │   ├── PipeNetworkViewModel.cs
│       │   ├── CalculationsViewModel.cs
│       │   ├── EvaluationViewModel.cs
│       │   └── ReportsViewModel.cs
│       ├── Views/                      # WPF XAML pages
│       │   ├── MainWindow.xaml
│       │   ├── DashboardView.xaml
│       │   ├── WaterSourceView.xaml
│       │   ├── ZonesView.xaml
│       │   ├── NozzleDatabaseView.xaml
│       │   ├── DripDatabaseView.xaml
│       │   ├── HeadsView.xaml
│       │   ├── PipeNetworkView.xaml
│       │   ├── CalculationsView.xaml
│       │   ├── EvaluationView.xaml
│       │   ├── ReportsView.xaml
│       │   └── InputDialog.xaml
│       ├── Services/
│       │   ├── DatabaseService.cs      # EF Core init + seeding
│       │   ├── LocalizationService.cs  # EN/AR language switching + RTL
│       │   ├── CsvImportService.cs     # Nozzle CSV import
│       │   ├── ReportService.cs        # Excel (ClosedXML) + PDF (QuestPDF)
│       │   ├── ProjectExportService.cs # Versioned JSON export/import
│       │   └── FileLoggerProvider.cs   # File-based logging
│       ├── Converters/ValueConverters.cs
│       ├── Themes/ModernTheme.xaml     # WPF resource dictionary
│       └── Resources/
│           ├── Strings.resx            # English strings
│           └── Strings.ar.resx         # Arabic strings (RTL)
│
├── tests/
│   └── IrrigationCalc.Tests/
│       ├── ZoneCalculatorTests.cs      # xUnit: PR, runtime, total flow
│       └── HydraulicsEngineTests.cs    # xUnit: Hazen-Williams, loop detect, velocities
│
└── docs/
    ├── sample_nozzles.csv              # CSV import sample
    └── README.md                       # This file
```

---

## Features

### 🏗 Project Management
- Create/delete projects with metadata
- "Load Sample Project" button for instant demo
- Versioned JSON export (`تصدير نسخة مشروع`) for backup & sharing

### 💧 Water Source
- Static pressure, available flow, elevation
- Automatic unit conversions displayed (L/min ↔ m³/h, bar ↔ kPa)

### 🗺 Zone Management
- Methods: **Spray / MP / Drip**
- Optional parent zone (hierarchical zones)
- Design pressure and target irrigation depth

### 💦 Nozzle Database
- Pre-loaded: **Hunter** (Pro-S, MP Rotator) + **Rain Bird** (1800 series, R-VAN)
- **CSV Import** for custom catalogs
- Filterable by brand and method

### 🌱 Drip Product Database
- Pre-loaded Hunter and Rain Bird drip lines
- Emitter flow, spacing, line spacing, pressure

### 🔩 Pipe Network
- Nodes: Source / Valve / Junction / HeadNode
- Segments: material (PVC/PE), diameter, length, fittings equivalent length
- EF Core persistence

### 📐 Calculations Engine (`IrrigationCalc` library)
| Calculation | Formula |
|---|---|
| Total zone flow | Σ head flows |
| Precipitation Rate | PR = 60 × Q / Area |
| Runtime | T = (depth / PR) × 60 |
| Hazen-Williams head loss | hf = 10.67 L Q^1.852 / (C^1.852 d^4.871) |
| Pressure at node | P_node = P_source − hf_cumulative ± elevation |

### ⚙ Design Checker
- **One-click "Design Check"** button (زر فحص التصميم)
- **Green/Yellow/Red** colour-coded dashboard
- Checks: velocity limits, pressure range, PR mismatch, flow vs. supply
- Configurable thresholds
- Severity: Info / Warning / Error
- Rating: **Good / Acceptable / Not Acceptable**

### 📄 Reports & Export
| Report | Excel | PDF |
|---|---|---|
| Zone Summary | ✅ | ✅ |
| Hydraulics | – | ✅ |
| BOM | ✅ | – |
| Project JSON | – | ✅ |

---

## Bilingual Support

| | English | Arabic |
|---|---|---|
| Toggle | Top bar language button | نفس الزر |
| Direction | LTR | RTL (automatic) |
| Font | Segoe UI | Segoe UI / Arabic Typesetting |
| All UI strings | `Strings.resx` | `Strings.ar.resx` |

---

## Database

- SQLite file location: `%AppData%\IrrigationDesigner\irrigation.db`
- Managed by **EF Core 8** with `EnsureCreated()`
- Entities: Project, WaterSource, Zone, Nozzle, DripProduct, Head, PipeNode, PipeSegment, Valve, ControllerStation

---

## Logging

Log files: `%AppData%\IrrigationDesigner\logs\app_YYYYMMDD.log`

---

## Assumptions & Limitations

1. **Tree networks only (v1):** The hydraulics engine supports tree (acyclic) pipe networks. Loops are detected and reported as an error with a clear message.
2. **Single water source per project.**
3. **Uniform head flow per zone:** Head-node demand for hydraulics is distributed equally across all HeadNodes. In production, each head's actual flow should be assigned to its node.
4. **Hazen-Williams C values:** PVC = 140, PE = 130 (defaults; overridable per segment).
5. **JSON import (v1):** JSON export is fully implemented. Import previews the envelope but does not recreate the project in v1 (full import planned for v2).
6. **No undo/redo** in v1.
7. **QuestPDF** uses Community License (free for internal/open-source use).
8. **ClosedXML** is used for all Excel generation (.xlsx format).
9. **Valve sizing** shows valve entity with size (mm); hydraulic valve loss is not modelled in v1 (included in fittings equivalent length).
10. **Elevation** affects computed pressure via: ΔP = Δh / 10.197 (hydrostatic).
