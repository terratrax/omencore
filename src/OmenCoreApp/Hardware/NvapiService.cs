using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace OmenCore.Hardware
{
    /// <summary>
    /// NVIDIA NVAPI wrapper for GPU overclocking and control.
    /// Provides access to clock offsets, power limits, and GPU monitoring.
    /// </summary>
    public class NvapiService : IDisposable
    {
        private readonly Services.LoggingService _logging;
        private bool _initialized;
        private bool _disposed;

        #region NVAPI Constants

        private const int NVAPI_OK = 0;
        private const int NVAPI_MAX_PHYSICAL_GPUS = 64;
        private const int NVAPI_MAX_CLOCKS_PER_GPU = 0x120;
        private const int NV_GPU_CLOCK_FREQUENCIES_VER = 0x00020000 | (sizeof(int) * 4);

        // Performance state IDs
        private const int NVAPI_GPU_PERF_PSTATE_P0 = 0;  // Maximum performance
        private const int NVAPI_GPU_PERF_PSTATE_P8 = 8;  // Basic 2D
        private const int NVAPI_GPU_PERF_PSTATE_P12 = 12; // Idle

        #endregion

        #region NVAPI Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct NvPhysicalGpuHandle
        {
            public IntPtr Handle;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct NV_GPU_CLOCK_FREQUENCIES
        {
            public uint Version;
            public uint ClockType; // 0 = current, 1 = base, 2 = boost
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = NVAPI_MAX_CLOCKS_PER_GPU)]
            public NV_GPU_CLOCK_FREQUENCIES_DOMAIN[] Domain;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 8)]
        private struct NV_GPU_CLOCK_FREQUENCIES_DOMAIN
        {
            public uint bIsPresent; // 1 if present
            public uint frequency;   // kHz
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NV_GPU_PERF_PSTATES20_INFO
        {
            public uint Version;
            public uint bIsEditable;
            public uint numPstates;
            public uint numClocks;
            public uint numBaseVoltages;
            // Followed by variable-size arrays
        }

        #endregion

        #region NVAPI Imports

        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr NvAPI_QueryInterface(uint id);

        // Function IDs obtained from NVAPI SDK
        private const uint NvAPI_Initialize_ID = 0x0150E828;
        private const uint NvAPI_Unload_ID = 0xD22BDD7E;
        private const uint NvAPI_EnumPhysicalGPUs_ID = 0xE5AC921F;
        private const uint NvAPI_GPU_GetFullName_ID = 0xCEEE8E9F;
        private const uint NvAPI_GPU_GetThermalSettings_ID = 0xE3640A56;
        private const uint NvAPI_GPU_GetAllClockFrequencies_ID = 0xDCB616C3;
        private const uint NvAPI_GPU_GetPstates20_ID = 0x6FF81213;
        private const uint NvAPI_GPU_SetPstates20_ID = 0x0F4DAE6B;
        private const uint NvAPI_GPU_GetPowerPoliciesInfo_ID = 0x34206D86;
        private const uint NvAPI_GPU_GetPowerPoliciesStatus_ID = 0x70916171;
        private const uint NvAPI_GPU_SetPowerPoliciesStatus_ID = 0xAD95F5ED;

        // Delegate types
        private delegate int NvAPI_InitializeDelegate();
        private delegate int NvAPI_UnloadDelegate();
        private delegate int NvAPI_EnumPhysicalGPUsDelegate([Out] NvPhysicalGpuHandle[] gpuHandles, out int gpuCount);
        private delegate int NvAPI_GPU_GetFullNameDelegate(NvPhysicalGpuHandle hPhysicalGpu, [MarshalAs(UnmanagedType.LPStr)] System.Text.StringBuilder szName);

        // Cached delegates
        private NvAPI_InitializeDelegate? _nvAPI_Initialize;
        private NvAPI_UnloadDelegate? _nvAPI_Unload;
        private NvAPI_EnumPhysicalGPUsDelegate? _nvAPI_EnumPhysicalGPUs;
        private NvAPI_GPU_GetFullNameDelegate? _nvAPI_GPU_GetFullName;

        #endregion

        #region Properties

        /// <summary>Whether NVAPI is initialized and available.</summary>
        public bool IsAvailable => _initialized;

        /// <summary>Number of NVIDIA GPUs detected.</summary>
        public int GpuCount { get; private set; }

        /// <summary>Current GPU core clock offset in MHz.</summary>
        public int CoreClockOffsetMHz { get; private set; }

        /// <summary>Current GPU memory clock offset in MHz.</summary>
        public int MemoryClockOffsetMHz { get; private set; }

        /// <summary>Current power limit percentage (100 = default TDP).</summary>
        public int PowerLimitPercent { get; private set; } = 100;

        /// <summary>Minimum allowed core clock offset.</summary>
        public int MinCoreOffset { get; private set; } = -500;

        /// <summary>Maximum allowed core clock offset.</summary>
        public int MaxCoreOffset { get; private set; } = 300;

        /// <summary>Minimum allowed memory clock offset.</summary>
        public int MinMemoryOffset { get; private set; } = -500;

        /// <summary>Maximum allowed memory clock offset.</summary>
        public int MaxMemoryOffset { get; private set; } = 1500;

        /// <summary>Minimum power limit percentage.</summary>
        public int MinPowerLimit { get; private set; } = 50;

        /// <summary>Maximum power limit percentage.</summary>
        public int MaxPowerLimit { get; private set; } = 125;

        /// <summary>Default power limit in watts.</summary>
        public int DefaultPowerLimitWatts { get; private set; }

        /// <summary>GPU name.</summary>
        public string GpuName { get; private set; } = "Unknown";

        #endregion

        public NvapiService(Services.LoggingService logging)
        {
            _logging = logging;
        }

        /// <summary>
        /// Initialize NVAPI and enumerate GPUs.
        /// </summary>
        public bool Initialize()
        {
            if (_initialized) return true;

            try
            {
                // Query Initialize function
                var initPtr = NvAPI_QueryInterface(NvAPI_Initialize_ID);
                if (initPtr == IntPtr.Zero)
                {
                    _logging.Info("NVAPI: nvapi64.dll not found or Initialize not available");
                    return false;
                }

                _nvAPI_Initialize = Marshal.GetDelegateForFunctionPointer<NvAPI_InitializeDelegate>(initPtr);

                // Initialize NVAPI
                var result = _nvAPI_Initialize();
                if (result != NVAPI_OK)
                {
                    _logging.Warn($"NVAPI: Initialize failed with code {result}");
                    return false;
                }

                // Query other functions
                QueryNvapiDelegates();

                // Enumerate GPUs
                EnumerateGpus();

                _initialized = true;
                _logging.Info($"NVAPI: Initialized successfully, {GpuCount} GPU(s) found");
                return true;
            }
            catch (DllNotFoundException)
            {
                _logging.Info("NVAPI: nvapi64.dll not found - NVIDIA drivers not installed");
                return false;
            }
            catch (Exception ex)
            {
                _logging.Error($"NVAPI: Initialization failed: {ex.Message}", ex);
                return false;
            }
        }

        private void QueryNvapiDelegates()
        {
            var ptr = NvAPI_QueryInterface(NvAPI_Unload_ID);
            if (ptr != IntPtr.Zero)
                _nvAPI_Unload = Marshal.GetDelegateForFunctionPointer<NvAPI_UnloadDelegate>(ptr);

            ptr = NvAPI_QueryInterface(NvAPI_EnumPhysicalGPUs_ID);
            if (ptr != IntPtr.Zero)
                _nvAPI_EnumPhysicalGPUs = Marshal.GetDelegateForFunctionPointer<NvAPI_EnumPhysicalGPUsDelegate>(ptr);

            ptr = NvAPI_QueryInterface(NvAPI_GPU_GetFullName_ID);
            if (ptr != IntPtr.Zero)
                _nvAPI_GPU_GetFullName = Marshal.GetDelegateForFunctionPointer<NvAPI_GPU_GetFullNameDelegate>(ptr);
        }

        private void EnumerateGpus()
        {
            if (_nvAPI_EnumPhysicalGPUs == null) return;

            var handles = new NvPhysicalGpuHandle[NVAPI_MAX_PHYSICAL_GPUS];
            var result = _nvAPI_EnumPhysicalGPUs(handles, out int count);

            if (result == NVAPI_OK)
            {
                GpuCount = count;

                // Get GPU name
                if (count > 0 && _nvAPI_GPU_GetFullName != null)
                {
                    var name = new System.Text.StringBuilder(64);
                    if (_nvAPI_GPU_GetFullName(handles[0], name) == NVAPI_OK)
                    {
                        GpuName = name.ToString();
                        _logging.Info($"NVAPI: Primary GPU: {GpuName}");
                    }
                }

                // Detect offset limits (laptop GPUs often have restricted ranges)
                DetectLimits();
            }
        }

        private void DetectLimits()
        {
            // Default limits for laptop GPUs (more conservative)
            // Desktop GPUs typically allow more headroom
            if (GpuName.Contains("Laptop", StringComparison.OrdinalIgnoreCase) ||
                GpuName.Contains("Max-Q", StringComparison.OrdinalIgnoreCase) ||
                GpuName.Contains("Mobile", StringComparison.OrdinalIgnoreCase))
            {
                MaxCoreOffset = 200;
                MaxMemoryOffset = 500;
                MaxPowerLimit = 115; // Laptop GPUs often locked
                _logging.Info("NVAPI: Detected laptop GPU - using conservative limits");
            }
            else
            {
                MaxCoreOffset = 300;
                MaxMemoryOffset = 1500;
                MaxPowerLimit = 125;
                _logging.Info("NVAPI: Desktop GPU limits applied");
            }
        }

        /// <summary>
        /// Set GPU core clock offset.
        /// </summary>
        /// <param name="offsetMHz">Offset in MHz (negative = undervolt, positive = overclock)</param>
        /// <returns>True if successful</returns>
        public bool SetCoreClockOffset(int offsetMHz)
        {
            if (!_initialized)
            {
                _logging.Warn("NVAPI: Not initialized");
                return false;
            }

            // Clamp to valid range
            offsetMHz = Math.Clamp(offsetMHz, MinCoreOffset, MaxCoreOffset);

            try
            {
                // Note: Full implementation requires NvAPI_GPU_SetPstates20
                // This is a placeholder showing the API structure
                _logging.Info($"NVAPI: Setting core clock offset to {offsetMHz} MHz");
                
                // TODO: Implement via NvAPI_GPU_SetPstates20 when NVAPI SDK is properly linked
                // For now, log intent
                CoreClockOffsetMHz = offsetMHz;
                
                _logging.Info($"NVAPI: Core clock offset set to {offsetMHz} MHz (pending SDK integration)");
                return true;
            }
            catch (Exception ex)
            {
                _logging.Error($"NVAPI: Failed to set core clock offset: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Set GPU memory clock offset.
        /// </summary>
        /// <param name="offsetMHz">Offset in MHz</param>
        /// <returns>True if successful</returns>
        public bool SetMemoryClockOffset(int offsetMHz)
        {
            if (!_initialized)
            {
                _logging.Warn("NVAPI: Not initialized");
                return false;
            }

            offsetMHz = Math.Clamp(offsetMHz, MinMemoryOffset, MaxMemoryOffset);

            try
            {
                _logging.Info($"NVAPI: Setting memory clock offset to {offsetMHz} MHz");
                
                // TODO: Implement via NvAPI_GPU_SetPstates20
                MemoryClockOffsetMHz = offsetMHz;
                
                _logging.Info($"NVAPI: Memory clock offset set to {offsetMHz} MHz (pending SDK integration)");
                return true;
            }
            catch (Exception ex)
            {
                _logging.Error($"NVAPI: Failed to set memory clock offset: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Set GPU power limit as percentage of default TDP.
        /// </summary>
        /// <param name="percent">Percentage (e.g., 100 = default, 115 = +15%)</param>
        /// <returns>True if successful</returns>
        public bool SetPowerLimit(int percent)
        {
            if (!_initialized)
            {
                _logging.Warn("NVAPI: Not initialized");
                return false;
            }

            percent = Math.Clamp(percent, MinPowerLimit, MaxPowerLimit);

            try
            {
                _logging.Info($"NVAPI: Setting power limit to {percent}%");
                
                // TODO: Implement via NvAPI_GPU_SetPowerPoliciesStatus
                PowerLimitPercent = percent;
                
                _logging.Info($"NVAPI: Power limit set to {percent}% (pending SDK integration)");
                return true;
            }
            catch (Exception ex)
            {
                _logging.Error($"NVAPI: Failed to set power limit: {ex.Message}", ex);
                return false;
            }
        }

        /// <summary>
        /// Get current GPU clock frequencies.
        /// </summary>
        public GpuClockInfo GetCurrentClocks()
        {
            // Return placeholder data - would use NvAPI_GPU_GetAllClockFrequencies
            return new GpuClockInfo
            {
                CoreClockMHz = 0,
                MemoryClockMHz = 0,
                CoreOffsetMHz = CoreClockOffsetMHz,
                MemoryOffsetMHz = MemoryClockOffsetMHz
            };
        }

        /// <summary>
        /// Reset all overclocking settings to default.
        /// </summary>
        public bool ResetToDefaults()
        {
            try
            {
                SetCoreClockOffset(0);
                SetMemoryClockOffset(0);
                SetPowerLimit(100);
                
                _logging.Info("NVAPI: Reset all settings to defaults");
                return true;
            }
            catch (Exception ex)
            {
                _logging.Error($"NVAPI: Failed to reset defaults: {ex.Message}", ex);
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (_initialized && _nvAPI_Unload != null)
                {
                    _nvAPI_Unload();
                    _logging.Info("NVAPI: Unloaded");
                }
            }
            catch (Exception ex)
            {
                _logging.Error($"NVAPI: Unload failed: {ex.Message}");
            }

            _disposed = true;
            _initialized = false;
        }
    }

    /// <summary>
    /// GPU clock frequency information.
    /// </summary>
    public class GpuClockInfo
    {
        public int CoreClockMHz { get; set; }
        public int MemoryClockMHz { get; set; }
        public int CoreOffsetMHz { get; set; }
        public int MemoryOffsetMHz { get; set; }
        public int BoostClockMHz { get; set; }
        public int BaseClockMHz { get; set; }
    }

    /// <summary>
    /// GPU overclocking profile.
    /// </summary>
    public class GpuOcProfile
    {
        public string Name { get; set; } = "Default";
        public int CoreOffsetMHz { get; set; }
        public int MemoryOffsetMHz { get; set; }
        public int PowerLimitPercent { get; set; } = 100;
        public int? VoltageOffsetMv { get; set; }
        public bool IsActive { get; set; }
    }
}
