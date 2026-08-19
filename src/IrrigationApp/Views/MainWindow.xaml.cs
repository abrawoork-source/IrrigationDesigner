// File: src/IrrigationApp/Views/MainWindow.xaml.cs
using IrrigationApp.Services;
using IrrigationApp.ViewModels;

namespace IrrigationApp.Views;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(DatabaseService.Instance);
    }
}
