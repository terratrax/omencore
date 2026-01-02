using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OmenCore.Avalonia.Services;

namespace OmenCore.Avalonia.ViewModels;

/// <summary>
/// Main window ViewModel handling navigation.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    private readonly IHardwareService _hardwareService;
    private readonly IConfigurationService _configService;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPage = "Dashboard";

    [ObservableProperty]
    private string _modelName = "HP OMEN";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isConnected = true;

    public DashboardViewModel DashboardVm { get; }
    public FanControlViewModel FanControlVm { get; }
    public SystemControlViewModel SystemControlVm { get; }
    public SettingsViewModel SettingsVm { get; }

    public MainWindowViewModel(
        IHardwareService hardwareService,
        IConfigurationService configService,
        DashboardViewModel dashboardVm,
        FanControlViewModel fanControlVm,
        SystemControlViewModel systemControlVm,
        SettingsViewModel settingsVm)
    {
        _hardwareService = hardwareService;
        _configService = configService;
        DashboardVm = dashboardVm;
        FanControlVm = fanControlVm;
        SystemControlVm = systemControlVm;
        SettingsVm = settingsVm;

        CurrentView = DashboardVm;
        
        Initialize();
    }

    private async void Initialize()
    {
        try
        {
            await _configService.LoadAsync();
            var capabilities = await _hardwareService.GetCapabilitiesAsync();
            ModelName = capabilities.ModelName;
            StatusText = "Connected";
            IsConnected = true;
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            IsConnected = false;
        }
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        CurrentView = DashboardVm;
        CurrentPage = "Dashboard";
    }

    [RelayCommand]
    private void NavigateToFanControl()
    {
        CurrentView = FanControlVm;
        CurrentPage = "Fan Control";
    }

    [RelayCommand]
    private void NavigateToSystemControl()
    {
        CurrentView = SystemControlVm;
        CurrentPage = "System Control";
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentView = SettingsVm;
        CurrentPage = "Settings";
    }
}
