// File: src/IrrigationApp/Services/LocalizationService.cs
using System.Globalization;
using System.Resources;
using System.Windows;
using System.Windows.Media;

namespace IrrigationApp.Services;

/// <summary>
/// Runtime language / RTL switching service.
/// Supported cultures: en (default) and ar (Arabic, RTL).
/// </summary>
public class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new LocalizationService();

    private readonly ResourceManager _rm =
        new("IrrigationApp.Resources.Strings", typeof(LocalizationService).Assembly);

    public string CurrentLanguage { get; private set; } = "en";
    public bool IsArabic => CurrentLanguage == "ar";

    public event Action? LanguageChanged;

    private LocalizationService() { }

    public void SetLanguage(string lang)
    {
        CurrentLanguage = lang;
        var culture = new CultureInfo(lang);
        CultureInfo.CurrentCulture   = culture;
        CultureInfo.CurrentUICulture = culture;
        LanguageChanged?.Invoke();
    }

    public string Get(string key)
    {
        try
        {
            return _rm.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        }
        catch
        {
            return key;
        }
    }

    public FlowDirection GetFlowDirection() =>
        IsArabic ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
}
