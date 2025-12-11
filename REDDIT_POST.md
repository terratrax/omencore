# OmenCore - Modern Open-Source Control Center for HP OMEN Laptops

**TL;DR**: I built a lightweight, privacy-respecting replacement for HP OMEN Gaming Hub. No bloat, no telemetry, full hardware control. Available now on GitHub.

🔗 **Website**: [omencore.info](https://omencore.info)  
📦 **GitHub**: [github.com/theantipopau/omencore](https://github.com/theantipopau/omencore)  
📥 **Download**: [Latest Release (v1.0.0.5)](https://github.com/theantipopau/omencore/releases/latest)

---

## 🎯 Why I Built This

HP OMEN Gaming Hub is a **500MB beast** that requires:
- Microsoft Store account + mandatory sign-in
- Constant background processes (3-5% CPU idle)
- Telemetry collection with unclear opt-out
- Bloated game library features most people ignore

**OmenCore** is **<50MB**, runs offline, and focuses on what matters: **thermal management, performance tuning, and RGB control**. Think of it as "MSI Afterburner for laptops" — if you want fan curves, undervolting, and hardware monitoring without the bloat, this is it.

---

## ✨ Key Features

### 🌡️ **Thermal Control**
- **Custom fan curves** with visual editor (e.g., 40°C→30%, 60°C→55%, 80°C→85%)
- **Real-time monitoring** with live temperature charts
- **System tray badge** shows live CPU temp on your notification icon (updates every 2s)
- **Per-fan telemetry** displays RPM and duty cycle for CPU + GPU fans

### ⚡ **Performance Tuning**
- **CPU undervolting** via Intel MSR (separate core/cache offsets, typical -100mV to -150mV)
- **Performance modes**: Balanced, Performance, Turbo (manages CPU/GPU wattage)
- **GPU mux switching**: Hybrid, Discrete (dGPU only), Integrated (iGPU only)
- **Respects external tools**: Detects ThrottleStop/Intel XTU and defers control automatically

### 💡 **RGB Lighting**
- **4-zone OMEN keyboards** with per-zone color/intensity
- **Effects**: Static, Breathing, Wave, Reactive
- **Peripheral sync** (work in progress - Corsair/Logitech support coming soon)

### 📊 **Hardware Monitoring**
- **Live telemetry**: CPU/GPU temp, load, clocks, VRAM, SSD temp
- **Smart polling**: Only updates UI when values change >0.5° (reduces overhead)
- **Low overhead mode**: Disables charts to drop CPU usage from ~2% to <0.5%

### 🧹 **System Cleanup**
- **OMEN Hub removal tool** - safely uninstalls HP Gaming Hub (Store packages, registry, tasks, etc.)
- **Creates system restore point** before destructive operations
- **Gaming Mode** - one-click optimization (disables animations, toggles services)

### 🔄 **Auto-Update**
- In-app update checker (polls GitHub every 6 hours)
- SHA256 verification for security
- One-click install with download progress

---

## 🖼️ Screenshots

[Insert screenshots here - main dashboard, fan curves, thermal charts, RGB editor]

---

## 📋 Feature Parity with HP Gaming Hub

| HP Gaming Hub Feature | OmenCore Status |
|----------------------|----------------|
| Fan Control | ✅ Full support |
| Performance Modes | ✅ Full support |
| CPU Undervolting | ✅ Full support |
| Keyboard RGB | ✅ Profiles (per-key editor v1.1) |
| Hardware Monitoring | ✅ Full support |
| Gaming Mode | ✅ Service toggles |
| Peripheral Control | ⚠️ Beta (coming soon) |
| Hub Cleanup | ✅ Exclusive feature |
| Network Booster | ❌ Out of scope |
| Game Library | ❌ Out of scope |
| Per-Game Profiles | 🔜 Planned v1.1 |
| In-Game Overlay | 🔜 Planned v1.2 |

**Verdict**: Covers **90% of daily usage** with better performance and privacy.

---

## 🚀 Getting Started

### Requirements
- **OS**: Windows 10 (19041+) or Windows 11
- **Laptop**: HP OMEN 15/16/17 series (2019-2024 models tested)
- **Runtime**: .NET 8 Desktop Runtime - [Download](https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe)
- **Driver** (optional): WinRing0 for hardware access (auto-installed via setup)

### Installation
1. Download installer from [Releases](https://github.com/theantipopau/omencore/releases/latest)
2. Run as Administrator
3. Select "Install WinRing0 driver" (recommended)
4. Launch and enjoy!

**⚠️ Windows Defender Note**: WinRing0 may be flagged as `HackTool:Win64/WinRing0` — this is a **known false positive** for hardware drivers. Add exclusion or verify digital signature.

---

## 🛠️ Tech Stack

- **.NET 8.0 WPF** (native Windows app, not Electron!)
- **LibreHardwareMonitor** for sensor polling
- **Direct EC access** for fan/LED control
- **Intel MSR** for CPU undervolting
- **Async/await** for responsive UI

Architecture follows MVVM with sub-ViewModels for modular development. Full source available on GitHub (MIT license).

---

## 🗺️ Roadmap

**v1.1 (Q1 2025)**
- Per-key RGB editor with visual grid
- Per-game profiles (auto-switch settings on game launch)
- Corsair iCUE integration (lighting, DPI, macros)
- Logitech G HUB integration

**v1.2 (Q2 2025)**
- In-game overlay (FPS, temps, load)
- Multi-language support (i18n)
- Dark/Light theme toggle
- Power draw tracking (CPU + GPU watts)

**v2.0 (Q3 2025)**
- Historical data export (CSV)
- Alert thresholds (notifications on overheat)
- Community plugin system
- Windows 11 Mica materials

---

## 🤝 Contributing

This is **open source** (MIT license) — contributions welcome!

- **Found a bug?** [Open an issue](https://github.com/theantipopau/omencore/issues)
- **Want to help?** Check [CONTRIBUTING.md](https://github.com/theantipopau/omencore/blob/main/CONTRIBUTING.md)
- **Have ideas?** Join discussions on GitHub

Especially looking for:
- Translations (Spanish, German, French, Chinese)
- Testing on 2019-2021 OMEN models
- Corsair/Logitech SDK integration help

---

## 📢 Spread the Word

If this helps you, please:
- ⭐ Star the repo on GitHub
- 🔄 Share with OMEN laptop owners
- 💬 Leave feedback or suggestions

---

## ❓ FAQ

**Q: Is this safe? Will it void my warranty?**  
A: OmenCore uses the same hardware APIs as HP Gaming Hub (EC registers, Intel MSR). No BIOS flashing or firmware modification. However, undervolting carries inherent risks — use conservative offsets and stress test. Warranty concerns vary by region.

**Q: Why not just use HP Gaming Hub?**  
A: If you're happy with Gaming Hub, keep using it! OmenCore is for users who want:
- No Microsoft Store account requirement
- Better performance (2-5% CPU savings)
- Privacy (no telemetry)
- Offline operation

**Q: Does it work on non-OMEN HP laptops?**  
A: Partially. Monitoring works on any laptop, but fan/RGB control requires OMEN-specific EC layout. Might expand support in future.

**Q: Can I use this with HP Gaming Hub installed?**  
A: Yes, but they'll conflict if both try to control fans simultaneously. Use the cleanup tool to remove Gaming Hub, or disable OmenCore's fan service.

**Q: What about desktops (OMEN 25L/30L/40L/45L)?**  
A: May work for monitoring, but desktop fan control is limited (usually via BIOS). Not officially tested.

---

## 🙏 Acknowledgments

Built with:
- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) - sensor library
- [Hardcodet.NotifyIcon.Wpf](https://github.com/hardcodet/wpf-notifyicon) - system tray
- [CUE.NET](https://github.com/DarthAffe/CUE.NET) - Corsair SDK wrapper
- Community feedback from r/HPOmen, r/GamingLaptops

Special thanks to early testers and contributors! 🎉

---

**Links**:
- 🌐 Website: [omencore.info](https://omencore.info)
- 📦 GitHub: [github.com/theantipopau/omencore](https://github.com/theantipopau/omencore)
- 📥 Download: [Latest Release](https://github.com/theantipopau/omencore/releases/latest)
- 📖 Documentation: [Quick Start Guide](https://github.com/theantipopau/omencore/blob/main/docs/QUICK_START.md)

---

*Posted from r/HPOmen, r/GamingLaptops, r/Windows*
