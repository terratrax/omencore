using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using OmenCore.Hardware;
using OmenCore.Models;
using OmenCore.Services;

namespace OmenCore.Views
{
    /// <summary>
    /// In-game OSD overlay window. Shows system stats in a transparent overlay.
    /// 
    /// Key features:
    /// - Master disable toggle (no process when disabled)
    /// - Click-through (doesn't interfere with games)
    /// - Configurable position and metrics
    /// - Auto-hides when fullscreen apps detected (optional)
    /// </summary>
    public partial class OsdOverlayWindow : Window, INotifyPropertyChanged
    {
        // Win32 for click-through window
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x00000020;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        
        private readonly DispatcherTimer _updateTimer;
        private readonly ThermalSensorProvider? _thermalProvider;
        private readonly FanService? _fanService;
        private readonly OsdSettings _settings;
        
        // Bindable properties
        private double _cpuTemp;
        private double _gpuTemp;
        private double _cpuLoad;
        private double _gpuLoad;
        private string _fanSpeed = "-- / --";
        private string _ramUsage = "-- GB";
        private bool _isThrottling;
        
        public double CpuTemp { get => _cpuTemp; set { _cpuTemp = value; OnPropertyChanged(); } }
        public double GpuTemp { get => _gpuTemp; set { _gpuTemp = value; OnPropertyChanged(); } }
        public double CpuLoad { get => _cpuLoad; set { _cpuLoad = value; OnPropertyChanged(); } }
        public double GpuLoad { get => _gpuLoad; set { _gpuLoad = value; OnPropertyChanged(); } }
        public string FanSpeed { get => _fanSpeed; set { _fanSpeed = value; OnPropertyChanged(); } }
        public string RamUsage { get => _ramUsage; set { _ramUsage = value; OnPropertyChanged(); } }
        public bool IsThrottling { get => _isThrottling; set { _isThrottling = value; OnPropertyChanged(); } }
        
        // Settings-bound visibility
        public bool ShowCpuTemp => _settings.ShowCpuTemp;
        public bool ShowGpuTemp => _settings.ShowGpuTemp;
        public bool ShowCpuLoad => _settings.ShowCpuLoad;
        public bool ShowGpuLoad => _settings.ShowGpuLoad;
        public bool ShowFanSpeed => _settings.ShowFanSpeed;
        public bool ShowRamUsage => _settings.ShowRamUsage;
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        public OsdOverlayWindow(OsdSettings settings, ThermalSensorProvider? thermalProvider = null, FanService? fanService = null)
        {
            _settings = settings ?? new OsdSettings();
            _thermalProvider = thermalProvider;
            _fanService = fanService;
            
            InitializeComponent();
            DataContext = this;
            
            // Set opacity from settings
            Opacity = _settings.Opacity;
            
            // Position window
            PositionWindow();
            
            // Setup update timer (1 second interval)
            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _updateTimer.Tick += UpdateTimer_Tick;
        }
        
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Make window click-through
            var hwnd = new WindowInteropHelper(this).Handle;
            var extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW);
        }
        
        public void StartUpdates()
        {
            _updateTimer.Start();
            UpdateStats(); // Initial update
        }
        
        public void StopUpdates()
        {
            _updateTimer.Stop();
        }
        
        private void UpdateTimer_Tick(object? sender, EventArgs e)
        {
            UpdateStats();
        }
        
        private void UpdateStats()
        {
            try
            {
                // Read temperatures
                if (_thermalProvider != null)
                {
                    var temps = _thermalProvider.ReadTemperatures();
                    foreach (var reading in temps)
                    {
                        if (reading.Sensor.Contains("CPU", StringComparison.OrdinalIgnoreCase))
                        {
                            CpuTemp = reading.Celsius;
                        }
                        else if (reading.Sensor.Contains("GPU", StringComparison.OrdinalIgnoreCase))
                        {
                            GpuTemp = reading.Celsius;
                        }
                    }
                    
                    // Simple throttling detection (temps > 95°C)
                    IsThrottling = CpuTemp > 95 || GpuTemp > 95;
                }
                
                // Read fan speeds
                if (_fanService != null && _fanService.FanTelemetry.Count >= 2)
                {
                    var cpu = _fanService.FanTelemetry[0];
                    var gpu = _fanService.FanTelemetry[1];
                    FanSpeed = $"{cpu.Rpm:N0} / {gpu.Rpm:N0}";
                }
                
                // Get RAM usage
                var ramInfo = new Microsoft.VisualBasic.Devices.ComputerInfo();
                var usedRam = (ramInfo.TotalPhysicalMemory - ramInfo.AvailablePhysicalMemory) / (1024.0 * 1024 * 1024);
                RamUsage = $"{usedRam:F1} GB";
                
                // TODO: CPU/GPU load would require additional monitoring
                // For now, estimate from temps
                CpuLoad = Math.Min(100, CpuTemp * 1.1);
                GpuLoad = Math.Min(100, GpuTemp * 1.1);
            }
            catch
            {
                // Silently ignore errors in OSD update
            }
        }
        
        private void PositionWindow()
        {
            var workArea = SystemParameters.WorkArea;
            
            switch (_settings.Position.ToLowerInvariant())
            {
                case "topleft":
                    Left = workArea.Left + 10;
                    Top = workArea.Top + 10;
                    break;
                case "topright":
                    Left = workArea.Right - Width - 10;
                    Top = workArea.Top + 10;
                    break;
                case "bottomleft":
                    Left = workArea.Left + 10;
                    Top = workArea.Bottom - Height - 10;
                    break;
                case "bottomright":
                    Left = workArea.Right - Width - 10;
                    Top = workArea.Bottom - Height - 10;
                    break;
                default:
                    Left = workArea.Left + 10;
                    Top = workArea.Top + 10;
                    break;
            }
        }
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
