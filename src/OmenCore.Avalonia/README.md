# OmenCore Avalonia GUI

Cross-platform graphical interface for OmenCore, built with [Avalonia UI](https://avaloniaui.net/).

## Overview

This is the Linux GUI for OmenCore, providing a modern, cross-platform interface for controlling HP OMEN laptops. It uses the same MVVM architecture as the Windows WPF application, making it familiar to contributors.

## Features

- **Dashboard**: Real-time system monitoring (temperatures, fan speeds, usage)
- **Fan Control**: Custom fan curves with presets
- **System Control**: Performance modes, GPU switching, keyboard lighting
- **Settings**: Application configuration and preferences

## Requirements

### Linux
- .NET 8.0 Runtime
- X11 or Wayland display server
- HP OMEN laptop with hp-wmi kernel module

### Build Requirements
- .NET 8.0 SDK
- (Optional) Avalonia templates: `dotnet new install Avalonia.Templates`

## Building

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Publish for Linux x64
dotnet publish -c Release -r linux-x64 --self-contained

# Publish for Linux ARM64
dotnet publish -c Release -r linux-arm64 --self-contained
```

## Running

```bash
# Development
dotnet run

# Published version
./publish/linux-x64/omencore-gui
```

## Architecture

```
OmenCore.Avalonia/
├── App.axaml              # Application resources and theme
├── App.axaml.cs           # Dependency injection setup
├── Program.cs             # Entry point
├── Services/
│   ├── IHardwareService.cs        # Hardware abstraction interface
│   ├── LinuxHardwareService.cs    # Linux sysfs implementation
│   ├── ConfigurationService.cs    # TOML config management
│   └── FanCurveService.cs         # Fan curve presets
├── ViewModels/
│   ├── MainWindowViewModel.cs     # Navigation
│   ├── DashboardViewModel.cs      # System monitoring
│   ├── FanControlViewModel.cs     # Fan curves
│   ├── SystemControlViewModel.cs  # Performance/GPU/RGB
│   └── SettingsViewModel.cs       # Configuration
├── Views/
│   ├── MainWindow.axaml           # Main layout
│   ├── DashboardView.axaml        # Dashboard UI
│   ├── FanControlView.axaml       # Fan control UI
│   ├── SystemControlView.axaml    # System control UI
│   └── SettingsView.axaml         # Settings UI
└── Themes/
    └── OmenTheme.axaml            # OMEN-styled dark theme
```

## Linux Hardware Support

The GUI uses the following Linux interfaces:

| Feature | Interface | Notes |
|---------|-----------|-------|
| Temperatures | `/sys/class/hwmon/*/temp*_input` | coretemp, amdgpu, nvidia |
| Fan speeds | `/sys/class/hwmon/*/fan*_input` | RPM readings |
| Fan control | hp-wmi / EC | Requires root or udev rules |
| Performance modes | `/sys/devices/platform/hp-wmi/thermal_profile` | quiet/balanced/performance |
| GPU switching | `prime-select` | NVIDIA Optimus |
| Keyboard backlight | `/sys/class/leds/hp::kbd_backlight` | Brightness and color |
| Battery | `/sys/class/power_supply/BAT*` | Percentage and status |

## Permissions

Some features require elevated permissions. Create a udev rule for non-root access:

```bash
# /etc/udev/rules.d/99-omencore.rules
SUBSYSTEM=="leds", ACTION=="add", KERNEL=="hp::kbd_backlight", RUN+="/bin/chmod 666 /sys/class/leds/hp::kbd_backlight/brightness"
```

Or run with `pkexec`:
```bash
pkexec ./omencore-gui
```

## Configuration

Config file location: `~/.config/omencore/config.toml`

```toml
# OmenCore Configuration
start_minimized = false
dark_theme = true
polling_interval_ms = 1000
auto_apply_profile = true
default_performance_mode = "balanced"
```

## Development Notes

### Avalonia vs WPF

| Concept | WPF | Avalonia |
|---------|-----|----------|
| XAML file extension | `.xaml` | `.axaml` |
| Resource syntax | `{StaticResource}` | Same |
| Binding | `{Binding}` | Same + compiled bindings |
| Styles | `<Style TargetType="">` | `<Style Selector="">` |
| Triggers | `<Style.Triggers>` | CSS-like selectors |

### Adding New Views

1. Create ViewModel in `ViewModels/`
2. Create `.axaml` and `.axaml.cs` in `Views/`
3. Register ViewModel in `App.axaml.cs` DI container
4. Add DataTemplate in `MainWindow.axaml`
5. Add navigation command in `MainWindowViewModel`

## License

MIT License - See main repository LICENSE file.
