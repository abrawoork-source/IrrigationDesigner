// File: src/IrrigationApp/Views/InputDialog.xaml.cs
using System.Windows;

namespace IrrigationApp.Views;

public partial class InputDialog : Window
{
    public string? Result { get; private set; }

    public InputDialog(string prompt, string title = "Input", string defaultValue = "")
    {
        InitializeComponent();
        Title            = title;
        PromptText.Text  = prompt;
        InputBox.Text    = defaultValue;
        Loaded          += (_, _) => InputBox.Focus();
    }

    private void OkButton_Click(object sender, RoutedEventArgs e)
    {
        Result        = InputBox.Text;
        DialogResult  = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    public static string? Show(string prompt, string title = "Input", string defaultValue = "")
    {
        var dlg = new InputDialog(prompt, title, defaultValue);
        return dlg.ShowDialog() == true ? dlg.Result : null;
    }
}
