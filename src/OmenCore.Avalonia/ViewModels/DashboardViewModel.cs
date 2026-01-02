using CommunityToolkit.Mvvm.ComponentModel;
using OmenCore.Avalonia.Services;

namespace OmenCore.Avalonia.ViewModels;

/// <summary>
/// Dashboard ViewModel showing system overview.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly IHardwareService _hardwareService;
    private bool _disposed;

    [ObservableProperty]
    private double _cpuTemperature;

    [ObservableProperty]
    private double _gpuTemperature;

    [ObservableProperty]
    private int _cpuFanRpm;

    [ObservableProperty]
    private int _gpuFanRpm;

    [ObservableProperty]
    private double _cpuUsage;

    [ObservableProperty]
    private double _gpuUsage;

    [ObservableProperty]
    private double _memoryUsage;

    [ObservableProperty]
    private double _powerConsumption;

    [ObservableProperty]
    private int _batteryPercentage = 100;

    [ObservableProperty]
    private bool _isOnBattery;

    [ObservableProperty]
    private string _cpuName = "Loading...";

    [ObservableProperty]
    private string _gpuName = "Loading...";

    [ObservableProperty]
    private string _performanceMode = "Balanced";

    // Temperature warnings
    public bool IsCpuTemperatureWarning => CpuTemperature >= 80;
    public bool IsGpuTemperatureWarning => GpuTemperature >= 85;
    public bool IsCpuTemperatureCritical => CpuTemperature >= 95;
    public bool IsGpuTemperatureCritical => GpuTemperature >= 95;

    public DashboardViewModel(IHardwareService hardwareService)
    {
        _hardwareService = hardwareService;
        _hardwareService.StatusChanged += OnStatusChanged;
        
        Initialize();
    }

    private async void Initialize()
    {
        try
        {
            var capabilities = await _hardwareService.GetCapabilitiesAsync();
            CpuName = capabilities.CpuName;
            GpuName = capabilities.GpuName;

            var mode = await _hardwareService.GetPerformanceModeAsync();
            PerformanceMode = mode.ToString();

            // Initial status update
            var status = await _hardwareService.GetStatusAsync();
            UpdateStatus(status);
        }
        catch
        {
            // Handle initialization errors gracefully
        }
    }

    private void OnStatusChanged(object? sender, HardwareStatus status)
    {
        UpdateStatus(status);
    }

    private void UpdateStatus(HardwareStatus status)
    {
        CpuTemperature = Math.Round(status.CpuTemperature, 1);
        GpuTemperature = Math.Round(status.GpuTemperature, 1);
        CpuFanRpm = status.CpuFanRpm;
        GpuFanRpm = status.GpuFanRpm;
        CpuUsage = Math.Round(status.CpuUsage, 1);
        GpuUsage = Math.Round(status.GpuUsage, 1);
        MemoryUsage = Math.Round(status.MemoryUsage, 1);
        PowerConsumption = Math.Round(status.PowerConsumption, 1);
        BatteryPercentage = status.BatteryPercentage;
        IsOnBattery = status.IsOnBattery;

        // Notify temperature warning properties
        OnPropertyChanged(nameof(IsCpuTemperatureWarning));
        OnPropertyChanged(nameof(IsGpuTemperatureWarning));
        OnPropertyChanged(nameof(IsCpuTemperatureCritical));
        OnPropertyChanged(nameof(IsGpuTemperatureCritical));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _hardwareService.StatusChanged -= OnStatusChanged;
            _disposed = true;
        }
    }
}
