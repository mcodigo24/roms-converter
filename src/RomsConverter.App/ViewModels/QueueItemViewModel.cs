using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using RomsConverter.Core.Conversion;

namespace RomsConverter.App.ViewModels;

public partial class QueueItemViewModel(string sourcePath) : ObservableObject
{
    public Guid Id { get; } = Guid.NewGuid();

    public string SourcePath { get; } = sourcePath;

    public string DisplayName { get; } = Path.GetFileName(sourcePath);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    [NotifyPropertyChangedFor(nameof(IsCompleted))]
    private ConversionStatus _status = ConversionStatus.Pending;

    [ObservableProperty]
    private double _percent;

    /// <summary>Mensaje de la conversión (error o detalle); null si no hay.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _message;

    /// <summary>Errores de validación previos a convertir; null si el ítem es válido.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsInvalid))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    [NotifyPropertyChangedFor(nameof(DisplayMessage))]
    private string? _validationMessage;

    public bool IsInvalid => ValidationMessage is not null;

    public bool IsCompleted => Status == ConversionStatus.Completed;

    public bool IsIndeterminate => Status is ConversionStatus.Validating or ConversionStatus.Extracting;

    public bool HasMessage => DisplayMessage is not null;

    public string? DisplayMessage => ValidationMessage ?? Message;

    public string StatusText => IsInvalid && Status == ConversionStatus.Pending
        ? "Inválido"
        : Status switch
        {
            ConversionStatus.Pending => "En espera",
            ConversionStatus.Validating => "Validando…",
            ConversionStatus.Extracting => "Extrayendo…",
            ConversionStatus.Converting => "Convirtiendo",
            ConversionStatus.Completed => "Completado",
            ConversionStatus.Failed => "Falló",
            ConversionStatus.Cancelled => "Cancelado",
            _ => string.Empty,
        };

    partial void OnMessageChanged(string? value) => OnPropertyChanged(nameof(DisplayMessage));
}
