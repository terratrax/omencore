using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace OmenCore.Services
{
    /// <summary>
    /// Service for intercepting the physical OMEN key on HP OMEN laptops.
    /// Uses low-level keyboard hook to detect the OMEN key press and allow
    /// custom actions instead of launching HP OMEN Gaming Hub.
    /// 
    /// The OMEN key typically sends one of:
    /// - VK_LAUNCH_APP2 (0xB7) - Media key for app launch
    /// - Custom OEM key code via HP WMI
    /// </summary>
    public class OmenKeyService : IDisposable
    {
        private readonly LoggingService _logging;
        private readonly ConfigurationService? _configService;
        private IntPtr _hookHandle = IntPtr.Zero;
        private LowLevelKeyboardProc? _hookProc;
        private bool _isEnabled = true;
        private bool _disposed;
        private OmenKeyAction _currentAction = OmenKeyAction.ToggleOmenCore;
        private string _externalAppPath = string.Empty;
        private DateTime _lastKeyPress = DateTime.MinValue;
        private const int DebounceMs = 300;

        // Common key codes for OMEN key
        private const int VK_LAUNCH_APP2 = 0xB7;  // Media key often used by OEM
        private const int VK_OEM_1 = 0xBA;
        private const int VK_OEM_OMEN = 0xFF;  // Some models use this

        // HP OMEN-specific scan codes (varies by model)
        private static readonly int[] OmenScanCodes = { 0xE045, 0xE046, 0x0046 };

        #region Win32 API

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        #endregion

        /// <summary>
        /// Fired when the OMEN key is pressed.
        /// </summary>
        public event EventHandler? OmenKeyPressed;

        /// <summary>
        /// Fired when the OMEN key is released.
        /// </summary>
        public event EventHandler? OmenKeyReleased;
        
        /// <summary>
        /// Fired to toggle OmenCore window visibility
        /// </summary>
        public event EventHandler? ToggleOmenCoreRequested;
        
        /// <summary>
        /// Fired to cycle performance modes
        /// </summary>
        public event EventHandler? CyclePerformanceRequested;
        
        /// <summary>
        /// Fired to cycle fan modes
        /// </summary>
        public event EventHandler? CycleFanModeRequested;
        
        /// <summary>
        /// Fired to toggle max cooling
        /// </summary>
        public event EventHandler? ToggleMaxCoolingRequested;

        /// <summary>
        /// Get or set whether OMEN key interception is enabled.
        /// When disabled, the key passes through to system normally.
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                _logging.Info($"OMEN key interception {(_isEnabled ? "enabled" : "disabled")}");
                SaveSettings();
            }
        }
        
        /// <summary>
        /// Get or set the action to perform when OMEN key is pressed
        /// </summary>
        public OmenKeyAction CurrentAction
        {
            get => _currentAction;
            set
            {
                _currentAction = value;
                _logging.Info($"OMEN key action set to: {value}");
                SaveSettings();
            }
        }
        
        /// <summary>
        /// Path to external application to launch (when action is LaunchExternalApp)
        /// </summary>
        public string ExternalAppPath
        {
            get => _externalAppPath;
            set
            {
                _externalAppPath = value;
                SaveSettings();
            }
        }

        /// <summary>
        /// Get whether the keyboard hook is active.
        /// </summary>
        public bool IsHookActive => _hookHandle != IntPtr.Zero;

        public OmenKeyService(LoggingService logging, ConfigurationService? configService = null)
        {
            _logging = logging;
            _configService = configService;
            LoadSettings();
        }

        /// <summary>
        /// Start intercepting the OMEN key.
        /// </summary>
        public bool StartInterception()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                _logging.Warn("OMEN key hook already active");
                return true;
            }

            try
            {
                _hookProc = HookCallback;
                
                using var curProcess = Process.GetCurrentProcess();
                using var curModule = curProcess.MainModule;
                if (curModule == null)
                {
                    _logging.Error("Failed to get main module for keyboard hook");
                    return false;
                }

                _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, 
                    GetModuleHandle(curModule.ModuleName), 0);

                if (_hookHandle == IntPtr.Zero)
                {
                    var error = Marshal.GetLastWin32Error();
                    _logging.Error($"Failed to set keyboard hook. Error code: {error}");
                    return false;
                }

                _logging.Info("✓ OMEN key interception started");
                return true;
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to start OMEN key interception: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Stop intercepting the OMEN key.
        /// </summary>
        public void StopInterception()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
                _hookProc = null;
                _logging.Info("OMEN key interception stopped");
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _isEnabled)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                bool isOmenKey = IsOmenKey(hookStruct.vkCode, hookStruct.scanCode);

                if (isOmenKey)
                {
                    int msg = wParam.ToInt32();
                    
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        // Debounce check
                        if ((DateTime.Now - _lastKeyPress).TotalMilliseconds < DebounceMs)
                        {
                            return new IntPtr(1); // Block duplicate
                        }
                        _lastKeyPress = DateTime.Now;
                        
                        _logging.Debug($"OMEN key detected: VK=0x{hookStruct.vkCode:X2}, Scan=0x{hookStruct.scanCode:X4}");
                        
                        // Fire event and execute action on a separate thread to avoid blocking the hook
                        Task.Run(() => 
                        {
                            OmenKeyPressed?.Invoke(this, EventArgs.Empty);
                            ExecuteAction();
                        });
                        
                        // Return non-zero to block the key from reaching other apps
                        // This prevents OMEN Gaming Hub from launching
                        return new IntPtr(1);
                    }
                    else if (msg == WM_KEYUP || msg == WM_SYSKEYUP)
                    {
                        Task.Run(() => OmenKeyReleased?.Invoke(this, EventArgs.Empty));
                        return new IntPtr(1);
                    }
                }
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }
        
        private void ExecuteAction()
        {
            _logging.Info($"OMEN key pressed - executing: {_currentAction}");
            
            switch (_currentAction)
            {
                case OmenKeyAction.ToggleOmenCore:
                    ToggleOmenCoreRequested?.Invoke(this, EventArgs.Empty);
                    break;
                    
                case OmenKeyAction.CyclePerformance:
                    CyclePerformanceRequested?.Invoke(this, EventArgs.Empty);
                    break;
                    
                case OmenKeyAction.CycleFanMode:
                    CycleFanModeRequested?.Invoke(this, EventArgs.Empty);
                    break;
                    
                case OmenKeyAction.ToggleMaxCooling:
                    ToggleMaxCoolingRequested?.Invoke(this, EventArgs.Empty);
                    break;
                    
                case OmenKeyAction.LaunchExternalApp:
                    LaunchExternalApplication();
                    break;
                    
                case OmenKeyAction.DoNothing:
                    // Key is blocked but no action taken
                    break;
            }
        }
        
        private void LaunchExternalApplication()
        {
            if (string.IsNullOrWhiteSpace(_externalAppPath))
            {
                _logging.Warn("OMEN key set to launch app but no path configured");
                return;
            }
            
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = _externalAppPath,
                    UseShellExecute = true
                });
                _logging.Info($"Launched external app: {_externalAppPath}");
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to launch external app '{_externalAppPath}': {ex.Message}");
            }
        }
        
        private void LoadSettings()
        {
            if (_configService == null) return;
            
            try
            {
                _isEnabled = _configService.Config.OmenKeyEnabled;
                _externalAppPath = _configService.Config.OmenKeyExternalApp ?? string.Empty;
                
                if (Enum.TryParse<OmenKeyAction>(_configService.Config.OmenKeyAction, out var action))
                {
                    _currentAction = action;
                }
                
                _logging.Info($"OMEN key settings loaded: Enabled={_isEnabled}, Action={_currentAction}");
            }
            catch (Exception ex)
            {
                _logging.Warn($"Failed to load OMEN key settings: {ex.Message}");
            }
        }
        
        private void SaveSettings()
        {
            if (_configService == null) return;
            
            try
            {
                _configService.Config.OmenKeyEnabled = _isEnabled;
                _configService.Config.OmenKeyAction = _currentAction.ToString();
                _configService.Config.OmenKeyExternalApp = _externalAppPath;
                _configService.Save(_configService.Config);
            }
            catch (Exception ex)
            {
                _logging.Warn($"Failed to save OMEN key settings: {ex.Message}");
            }
        }

        private bool IsOmenKey(uint vkCode, uint scanCode)
        {
            // Check virtual key codes commonly used for OMEN key
            if (vkCode == VK_LAUNCH_APP2)
            {
                // This is a common key for OEM application launch
                // We need to verify it's the OMEN key specifically
                // by checking the scan code
                foreach (var omenScan in OmenScanCodes)
                {
                    if (scanCode == omenScan) return true;
                }
            }

            // Some OMEN models use a dedicated virtual key
            if (vkCode == VK_OEM_OMEN)
            {
                return true;
            }

            // Log unknown keys in debug mode to help identify OMEN key on different models
            // Uncomment this to discover the OMEN key code on your specific laptop:
            // _logging.Debug($"Key press: VK=0x{vkCode:X2}, Scan=0x{scanCode:X4}");

            return false;
        }

        /// <summary>
        /// Enable logging of all key presses to identify the OMEN key code.
        /// Use this during development to find the correct key code for your laptop model.
        /// </summary>
        public void EnableKeyDiscoveryMode(int durationSeconds = 30)
        {
            _logging.Info($"Key discovery mode enabled for {durationSeconds} seconds. Press keys to see their codes...");
            
            var originalHook = _hookProc;
            _hookProc = (nCode, wParam, lParam) =>
            {
                if (nCode >= 0)
                {
                    var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
                    int msg = wParam.ToInt32();
                    
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        _logging.Info($"[KEY DISCOVERY] VK=0x{hookStruct.vkCode:X2}, Scan=0x{hookStruct.scanCode:X4}, Flags=0x{hookStruct.flags:X}");
                    }
                }
                return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
            };

            // Restore normal hook after duration
            Task.Delay(TimeSpan.FromSeconds(durationSeconds)).ContinueWith(_ =>
            {
                _hookProc = originalHook;
                _logging.Info("Key discovery mode ended");
            });
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                StopInterception();
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Actions that can be bound to the OMEN key
    /// </summary>
    public enum OmenKeyAction
    {
        /// <summary>Show/hide OmenCore window</summary>
        ToggleOmenCore,
        
        /// <summary>Cycle through performance modes</summary>
        CyclePerformance,
        
        /// <summary>Cycle through fan presets</summary>
        CycleFanMode,
        
        /// <summary>Toggle max cooling on/off</summary>
        ToggleMaxCooling,
        
        /// <summary>Launch a user-specified external application</summary>
        LaunchExternalApp,
        
        /// <summary>Suppress the key but do nothing</summary>
        DoNothing
    }
}
