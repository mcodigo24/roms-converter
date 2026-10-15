using CommunityToolkit.Mvvm.ComponentModel;

namespace RomsConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Roms Converter";
}
