# OmenCore v1.5.0-beta4 Changelog

**Release Date:** December 18, 2025  
**Status:** Beta 4  
**Previous Version:** v1.5.0-beta3

---

## 🔧 Critical Bug Fixes

### GPU Power Boost Reset When Applying Fan Presets
**Files Changed:**
- `Hardware/WmiFanController.cs`

**Problem:** GPU Power Boost set to Maximum (175W TGP on RTX 4090) was being silently reset to Medium (~45W) whenever a fan preset was applied. This caused **~4x GPU performance loss** during gaming.

**Root Cause:** `WmiFanController.ApplyPreset()` called `ApplyGpuPowerFromPreset()` which set GPU power based on preset name:
- "performance", "turbo", "gaming" presets → Maximum
- "quiet", "silent", "battery" presets → Minimum  
- **All other presets (Auto, Max, Custom) → Medium** ❌

This meant applying common presets like "Auto" or "Max" would override the user's explicitly set GPU Power Boost level.

**Fix:** Removed `ApplyGpuPowerFromPreset()` method entirely. GPU Power Boost is now controlled **independently** via the System tab and is not affected by fan preset changes.

**User Impact:** 
- GPU Power Boost setting now persists correctly across fan preset changes
- RTX 4090 users report sustained 160-175W instead of being throttled to 45W
- Dramatic improvement in gaming performance for affected users

---

### Worker Race Condition on Startup (from beta3)
**Files Changed:**
- `Hardware/LibreHardwareMonitorImpl.cs`
- `Hardware/HardwareWorkerClient.cs`

**Problem:** On some systems, OmenCore would hang or show 0°C temps immediately after boot due to hardware worker not being ready.

**Fix:**
- Added `_workerInitializing` flag to track async initialization state
- `ReadSampleAsync` now waits up to 2 seconds for worker initialization
- Increased connection timeout from 3s to 5s
- Added 3 retry attempts for worker connection with 1s delay between retries

---

### Tray Icon Not Appearing After Reboot (from beta3)
**Files Changed:**
- `App.xaml.cs`

**Problem:** After Windows boot, the system tray icon sometimes failed to appear even though OmenCore was running.

**Root Cause:** Windows Explorer may not be fully ready to receive tray icon registrations immediately after boot.

**Fix:** Added `EnsureTrayIconVisibleAsync()` method that toggles icon visibility at 3s and 8s after startup, forcing Windows to re-register the icon.

---

## � New Features

### Preliminary Razer Chroma Support
**Files Added:**
- `Razer/RazerDevice.cs`
- `Razer/RazerDeviceType.cs`
- `Razer/RazerDeviceStatus.cs`
- `Razer/RazerService.cs`

**Files Changed:**
- `ViewModels/LightingViewModel.cs`
- `ViewModels/MainViewModel.cs`
- `Views/LightingView.xaml`

**Description:** Added a new Razer Devices section to the RGB Lighting tab. This is preliminary support that detects if Razer Synapse is running and provides placeholder UI for future Chroma SDK integration.

**Features:**
- Detects Razer Synapse process on system
- Shows "SYNAPSE DETECTED" badge when available
- Static color controls (RGB sliders)
- Breathing effect button
- Spectrum cycling effect button
- Device list placeholder for future SDK integration

**Note:** This is a foundation for full Razer Chroma SDK integration in a future release. Currently shows placeholder functionality only.

---

## 🔧 Bug Fixes

### Corsair Device Status Display
**Files Changed:**
- `Corsair/CorsairDeviceStatus.cs`

**Problem:** Corsair device info in the RGB tab showed "OmenCore.Corsair.CorsairDeviceStatus" instead of meaningful device information.

**Root Cause:** The `CorsairDeviceStatus` class had no `ToString()` override, so WPF binding displayed the class name.

**Fix:** Added `ToString()` override that shows connection type, polling rate, battery percent, and firmware version in a user-friendly format (e.g., "USB • 1000Hz • FW 1.2.3").

---

### Corsair Dark Core RGB PRO Misidentification
**Files Changed:**
- `Services/Corsair/CorsairHidDirect.cs`

**Problem:** Dark Core RGB PRO mouse was incorrectly identified as "Scimitar RGB Elite" on some systems.

**Root Cause:** HidSharp library was reporting PID 0x1BF0 for Dark Core RGB PRO, which was mapped to Scimitar RGB Elite in the device database.

**Fix:** Remapped PID 0x1BF0 to "Dark Core RGB PRO" since real-world testing confirmed this is the correct device for that PID.

---

### PowerModeChanged Event Log Spam
**Files Changed:**
- `Services/PowerAutomationService.cs`

**Problem:** The log was being spammed every ~1 second with "Power state unchanged, skipping profile application" messages, cluttering diagnostic logs.

**Root Cause:** `SystemEvents.PowerModeChanged` fires for many events (battery % changes, etc.), and each was logged at Info level even when no action was taken.

**Fix:** Changed unchanged power state logging from `Info` to `Debug` level. Only actual power state changes (AC plug/unplug) are now logged at Info level.

---

## �🏗️ Code Quality Improvements

### Build Warnings Eliminated
**Files Changed:**
- `ViewModels/MainViewModel.cs`
- `Hardware/MsrAccessFactory.cs`
- `OmenCore.HardwareWorker/Program.cs`

**Changes:**
- Fixed null reference warnings with proper null-forgiving operators
- Suppressed intentional WinRing0 obsolete warnings (kept as fallback for systems without PawnIO)
- Added `[SupportedOSPlatform("windows")]` attribute to worker process
- Build now completes with **0 warnings, 0 errors**

---

### Intel Arc GPU Detection Improvements
**Files Changed:**
- `Hardware/LibreHardwareMonitorImpl.cs`
- `OmenCore.HardwareWorker/Program.cs`

**Note:** LibreHardwareMonitor does not yet fully support Intel Arc GPUs. These changes improve detection and logging for future compatibility.

**Changes:**
- Added GPU enumeration logging on startup for diagnostics
- Intel Arc GPUs are now included in safe update check (same protection as NVIDIA/AMD)
- Added power/clock sensor reading for Intel Arc in worker process
- Better diagnostic logging when Arc sensors are unavailable

**Example Log Output:**
```
[GPU Detected] Intel Arc: Intel Arc A770
  - Temp sensors: [GPU Core=45°C]
  - Load sensors: [GPU Core=12%]
  - Power sensors: [GPU Power=35.2W]
```

---

## 📋 Files Changed Summary

| File | Changes |
|------|---------|
| `Hardware/WmiFanController.cs` | Removed `ApplyGpuPowerFromPreset()` - GPU power no longer overridden by fan presets |
| `Hardware/LibreHardwareMonitorImpl.cs` | Added `LogDetectedGpus()`, improved Intel Arc handling |
| `Hardware/MsrAccessFactory.cs` | Suppressed intentional obsolete warnings |
| `Hardware/HardwareWorkerClient.cs` | Increased timeouts, added retry logic (from beta3) |
| `OmenCore.HardwareWorker/Program.cs` | Added `[SupportedOSPlatform]`, GPU logging, Intel Arc support |
| `ViewModels/MainViewModel.cs` | Fixed null reference warnings |
| `App.xaml.cs` | Added tray icon visibility retry (from beta3) |
| `installer/OmenCoreInstaller.iss` | Version bump to 1.5.0-beta4 |
| `VERSION.txt` | Updated to 1.5.0-beta4 |

---

## 🧪 Testing Checklist

- [x] Build completes with 0 warnings, 0 errors
- [x] GPU Power Boost persists when changing fan presets
- [x] RTX 4090 sustains 160-175W during benchmarks
- [ ] Worker process starts reliably after boot
- [ ] Tray icon appears after reboot
- [ ] Intel Arc systems log GPU detection (even if sensors unavailable)

---

## 📦 Installation

**Installer:** `OmenCoreSetup-1.5.0-beta4.exe`

**Upgrade Notes:**
- Uninstall previous beta first (recommended) or install over existing
- GPU Power Boost settings are preserved in config
- Worker process will be updated automatically

---

## 🔍 Known Issues

1. **Intel Arc GPU sensors** - LibreHardwareMonitor doesn't fully support Intel Arc yet. Temperature/power readings may be unavailable on some Arc systems.

2. **GPU Power Limit slider** - MSI Afterburner-style power limit adjustment is not available on most laptops due to NVIDIA driver restrictions. Use the existing GPU Power Boost (Maximum) setting instead.

---

## 📝 Changelog from Beta3

This release focuses on stability and fixing the critical GPU power bug:

1. **GPU Power Fix** - The main improvement. Users with NVIDIA GPUs should see dramatically better gaming performance if they were affected by the preset bug.

2. **Build Quality** - Zero warnings builds for cleaner CI/CD.

3. **Intel Arc Preparation** - Groundwork for future Arc support when LibreHardwareMonitor adds it.
