using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RomsConverter.Core.Archives;
using RomsConverter.Core.Conversion;
using RomsConverter.Core.Processes;

namespace RomsConverter.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IJobValidator _validator = new JobValidator();
    private readonly ConversionQueue _queue;
    private CancellationTokenSource? _cts;

    public MainViewModel()
    {
        var locator = new ToolLocator();
        var runner = new ProcessRunner();
        ToolsMessage = locator.DescribeMissing();
        _queue = new ConversionQueue(
            _validator,
            new ArchiveExtractor(),
            console => console == ConsoleKind.PlayStation
                ? new ChdConverter(runner, locator.ChdmanPath)
                : new CsoConverter(runner, locator.MaxcsoPath));
    }

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

    public string AcceptedFormatsText => SelectedConsole switch
    {
        ConsoleKind.PlayStation => ".cue + .bin, o comprimidos .zip / .7z / .rar",
        ConsoleKind.Psp => ".iso, o comprimidos .zip / .7z / .rar",
        _ => string.Empty,
    };

    /// <summary>Mensaje accionable si faltan chdman.exe / maxcso.exe; null si están.</summary>
    public string? ToolsMessage { get; }

    public bool HasToolsMessage => ToolsMessage is not null;

    public ObservableCollection<QueueItemViewModel> Items { get; } = [];

    /// <summary>Último aviso al agregar archivos (rechazos, duplicados); null si no hay.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNotice))]
    private string? _notice;

    public bool HasNotice => Notice is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConvertCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(ChangeConsoleCommand))]
    [NotifyPropertyChangedFor(nameof(CanEdit))]
    private bool _isRunning;

    public bool CanEdit => !IsRunning;

    [RelayCommand]
    private void SelectConsole(ConsoleKind console)
    {
        SelectedConsole = console;
        OnPropertyChanged(nameof(AcceptedFormatsText));
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void ChangeConsole()
    {
        Items.Clear();
        Notice = null;
        SelectedConsole = null;
        ConvertCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Agrega archivos soltados o elegidos; rechaza los que no corresponden a la consola.</summary>
    public void AddPaths(IEnumerable<string> paths)
    {
        if (SelectedConsole is not { } console || IsRunning) return;

        var list = paths.ToList();
        var dropHasGameFile = list.Any(p => JobValidator.IsAcceptedSource(console, p) && !ArchiveFileTypes.IsArchive(p));
        var notices = new List<string>();

        foreach (var path in list)
        {
            var name = Path.GetFileName(path);

            if (Directory.Exists(path))
            {
                notices.Add($"{name}: las carpetas no se admiten; soltá los archivos.");
                continue;
            }

            // Los .bin viajan con su .cue: no se agregan por separado.
            if (console == ConsoleKind.PlayStation && IsBin(path) && dropHasGameFile)
                continue;

            if (!JobValidator.IsAcceptedSource(console, path))
            {
                notices.Add(IsBin(path)
                    ? $"{name}: agregá el .cue; el .bin se toma a partir de él."
                    : $"{name}: no corresponde a {SelectedConsoleName} (se admite {AcceptedFormatsText}).");
                continue;
            }

            if (Items.Any(i => string.Equals(i.SourcePath, path, StringComparison.OrdinalIgnoreCase)))
            {
                notices.Add($"{name}: ya está en la cola.");
                continue;
            }

            var item = new QueueItemViewModel(path, console);
            Validate(item, console);
            Items.Add(item);
        }

        Notice = notices.Count == 0 ? null : string.Join(Environment.NewLine, notices);
        ConvertCommand.NotifyCanExecuteChanged();
    }

    /// <summary>Cambia la ruta de salida de un ítem y lo revalida (y a los demás, por duplicados).</summary>
    public void SetOutputPath(QueueItemViewModel item, string outputPath)
    {
        if (SelectedConsole is not { } console || IsRunning) return;

        item.OutputPath = outputPath;
        foreach (var other in Items.Where(i => !i.IsCompleted))
            Validate(other, console);
        ConvertCommand.NotifyCanExecuteChanged();
    }

    private static bool IsBin(string path) =>
        string.Equals(Path.GetExtension(path), ".bin", StringComparison.OrdinalIgnoreCase);

    private void Validate(QueueItemViewModel item, ConsoleKind console)
    {
        var result = _validator.Validate(new ConversionJob(item.Id, console, item.SourcePath, item.OutputPath));
        var errors = result.Errors.ToList();

        // Dos ítems no pueden escribir en el mismo archivo: el primero de la cola se lo queda.
        var clash = Items
            .TakeWhile(i => i.Id != item.Id)
            .FirstOrDefault(i => string.Equals(
                Path.GetFullPath(i.OutputPath), Path.GetFullPath(item.OutputPath), StringComparison.OrdinalIgnoreCase));
        if (clash is not null)
            errors.Add($"Otra entrada ({clash.DisplayName}) ya usa la misma salida: {Path.GetFileName(item.OutputPath)}");

        item.ValidationMessage = errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
    }

    private bool CanConvert() =>
        !IsRunning
        && !HasToolsMessage
        && Items.Any(i => !i.IsCompleted && !i.IsInvalid);

    [RelayCommand(CanExecute = nameof(CanConvert))]
    private async Task ConvertAsync()
    {
        if (SelectedConsole is not { } console) return;

        var runnable = Items.Where(i => !i.IsCompleted && !i.IsInvalid).ToList();
        foreach (var item in runnable)
        {
            item.Status = ConversionStatus.Pending;
            item.Percent = 0;
            item.Message = null;
        }

        var jobs = runnable
            .Select(i => new ConversionJob(i.Id, console, i.SourcePath, i.OutputPath))
            .ToList();

        _cts = new CancellationTokenSource();
        IsRunning = true;
        try
        {
            await _queue.RunAsync(jobs, new Progress<ConversionProgress>(Apply), _cts.Token);
        }
        finally
        {
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
            ConvertCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel() => _cts?.Cancel();

    private void Apply(ConversionProgress progress)
    {
        var item = Items.FirstOrDefault(i => i.Id == progress.JobId);
        if (item is null) return;

        item.Status = progress.Status;
        if (progress.Percent is { } percent) item.Percent = percent;
        if (progress.Status == ConversionStatus.Completed) item.Percent = 100;
        if (progress.Message is not null) item.Message = progress.Message;
    }
}
