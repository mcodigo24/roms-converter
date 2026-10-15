using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomsConverter.Core.Conversion;

namespace RomsConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Roms Converter";

    /// <summary>Consola elegida; null mientras se muestra la pantalla de selección.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsConsoleSelected))]
    [NotifyPropertyChangedFor(nameof(IsConsoleSelection))]
    [NotifyPropertyChangedFor(nameof(SelectedConsoleName))]
    private ConsoleKind? _selectedConsole;

    public bool IsConsoleSelected => SelectedConsole is not null;

    public bool IsConsoleSelection => SelectedConsole is null;

    public string SelectedConsoleName => SelectedConsole switch
    {
        ConsoleKind.PlayStation => "PlayStation",
        ConsoleKind.Psp => "PSP",
        _ => string.Empty,
    };

    [RelayCommand]
    private void SelectConsole(ConsoleKind console) => SelectedConsole = console;
}
