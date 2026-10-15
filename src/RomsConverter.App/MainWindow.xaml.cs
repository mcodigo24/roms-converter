using System.IO;
using System.Windows;
using Microsoft.Win32;
using RomsConverter.App.ViewModels;
using RomsConverter.Core.Conversion;

namespace RomsConverter.App;

/// <summary>
/// Solo maneja diálogos y eventos de arrastrar/soltar; la lógica vive en <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainViewModel ViewModel => (MainViewModel)DataContext;

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = ViewModel.CanEdit && e.Data.GetDataPresent(DataFormats.FileDrop)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths)
            ViewModel.AddPaths(paths);
        e.Handled = true;
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var filter = ViewModel.SelectedConsole == ConsoleKind.Psp
            ? "Juegos PSP (*.iso;*.zip;*.7z;*.rar)|*.iso;*.zip;*.7z;*.rar"
            : "Juegos PlayStation (*.cue;*.zip;*.7z;*.rar)|*.cue;*.zip;*.7z;*.rar";

        var dialog = new OpenFileDialog
        {
            Multiselect = true,
            Filter = filter + "|Todos los archivos (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) == true)
            ViewModel.AddPaths(dialog.FileNames);
    }

    private void ChangeOutput_Click(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not QueueItemViewModel item) return;

        var extension = OutputPaths.ExtensionFor(ViewModel.SelectedConsole ?? ConsoleKind.PlayStation);
        var dialog = new SaveFileDialog
        {
            Title = "Archivo de salida",
            InitialDirectory = Path.GetDirectoryName(item.OutputPath),
            FileName = Path.GetFileName(item.OutputPath),
            DefaultExt = extension,
            AddExtension = true,
            OverwritePrompt = false, // si existe, el ítem queda inválido: nunca se sobrescribe
            Filter = $"Archivo {extension} (*{extension})|*{extension}",
        };

        if (dialog.ShowDialog(this) == true)
            ViewModel.SetOutputPath(item, dialog.FileName);
    }
}
