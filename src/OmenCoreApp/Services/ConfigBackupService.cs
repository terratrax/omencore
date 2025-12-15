using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Win32;
using OmenCore.Models;

namespace OmenCore.Services
{
    /// <summary>
    /// Service for importing/exporting the complete OmenCore configuration.
    /// Allows users to backup and restore all settings.
    /// </summary>
    public class ConfigBackupService
    {
        private readonly LoggingService _logging;
        private readonly ConfigurationService _configService;

        public ConfigBackupService(LoggingService logging, ConfigurationService configService)
        {
            _logging = logging;
            _configService = configService;
        }

        /// <summary>
        /// Export the complete configuration to a JSON file
        /// </summary>
        public async Task<bool> ExportConfigurationAsync()
        {
            try
            {
                var dialog = new SaveFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Export OmenCore Configuration",
                    FileName = $"omencore-config-backup-{DateTime.Now:yyyy-MM-dd-HHmmss}.json"
                };

                if (dialog.ShowDialog() != true)
                    return false;

                var backup = new ConfigBackup
                {
                    ExportDate = DateTime.Now,
                    Version = GetAppVersion(),
                    Config = _configService.Config
                };

                var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNamingPolicy = null // Keep original casing
                });

                await File.WriteAllTextAsync(dialog.FileName, json);

                _logging.Info($"📤 Configuration exported to: {dialog.FileName}");
                
                System.Windows.MessageBox.Show(
                    $"Configuration exported successfully!\n\nFile: {Path.GetFileName(dialog.FileName)}",
                    "Export Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to export configuration: {ex.Message}", ex);
                System.Windows.MessageBox.Show(
                    $"Failed to export configuration:\n{ex.Message}",
                    "Export Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Import configuration from a JSON file
        /// </summary>
        public async Task<bool> ImportConfigurationAsync(bool mergeWithExisting = false)
        {
            try
            {
                var dialog = new OpenFileDialog
                {
                    Filter = "JSON Files (*.json)|*.json|All Files (*.*)|*.*",
                    Title = "Import OmenCore Configuration"
                };

                if (dialog.ShowDialog() != true)
                    return false;

                var json = await File.ReadAllTextAsync(dialog.FileName);
                
                var backup = JsonSerializer.Deserialize<ConfigBackup>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (backup?.Config == null)
                {
                    System.Windows.MessageBox.Show(
                        "Invalid configuration file - no config data found.",
                        "Import Failed",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return false;
                }

                // Confirm import
                var result = System.Windows.MessageBox.Show(
                    $"Import configuration from:\n{Path.GetFileName(dialog.FileName)}\n\n" +
                    $"Exported on: {backup.ExportDate:yyyy-MM-dd HH:mm}\n" +
                    $"Version: {backup.Version}\n\n" +
                    (mergeWithExisting 
                        ? "This will MERGE with your current settings." 
                        : "This will REPLACE all current settings.") +
                    "\n\nContinue?",
                    "Confirm Import",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Question);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return false;

                if (mergeWithExisting)
                {
                    MergeConfiguration(backup.Config);
                }
                else
                {
                    // Full replace
                    _configService.Replace(backup.Config);
                }

                _configService.Save(_configService.Config);
                
                _logging.Info($"📥 Configuration imported from: {dialog.FileName}");
                
                System.Windows.MessageBox.Show(
                    "Configuration imported successfully!\n\n" +
                    "Note: Some settings may require restarting OmenCore to take effect.",
                    "Import Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to import configuration: {ex.Message}", ex);
                System.Windows.MessageBox.Show(
                    $"Failed to import configuration:\n{ex.Message}",
                    "Import Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Merge imported config with existing (preserves existing values not in import)
        /// </summary>
        private void MergeConfiguration(AppConfig imported)
        {
            var current = _configService.Config;

            // Merge fan presets (add new ones, skip existing by name)
            foreach (var preset in imported.FanPresets)
            {
                if (!current.FanPresets.Exists(p => p.Name == preset.Name))
                {
                    current.FanPresets.Add(preset);
                }
            }

            // Merge performance modes
            foreach (var mode in imported.PerformanceModes)
            {
                if (!current.PerformanceModes.Exists(m => m.Name == mode.Name))
                {
                    current.PerformanceModes.Add(mode);
                }
            }

            // Merge lighting profiles
            foreach (var profile in imported.LightingProfiles)
            {
                if (!current.LightingProfiles.Exists(p => p.Name == profile.Name))
                {
                    current.LightingProfiles.Add(profile);
                }
            }

            // Merge Corsair lighting presets
            foreach (var preset in imported.CorsairLightingPresets)
            {
                if (!current.CorsairLightingPresets.Exists(p => p.Name == preset.Name))
                {
                    current.CorsairLightingPresets.Add(preset);
                }
            }

            // Update scalar settings if they differ from defaults
            if (imported.Undervolt != null)
            {
                current.Undervolt = imported.Undervolt;
            }

            if (imported.Monitoring != null)
            {
                current.Monitoring = imported.Monitoring;
            }

            if (imported.FanHysteresis != null)
            {
                current.FanHysteresis = imported.FanHysteresis;
            }

            if (imported.Osd != null)
            {
                current.Osd = imported.Osd;
            }

            if (imported.Battery != null)
            {
                current.Battery = imported.Battery;
            }

            if (imported.PowerAutomation != null)
            {
                current.PowerAutomation = imported.PowerAutomation;
            }
            
            // OMEN key settings
            current.OmenKeyEnabled = imported.OmenKeyEnabled;
            current.OmenKeyIntercept = imported.OmenKeyIntercept;
            current.OmenKeyAction = imported.OmenKeyAction;
            current.OmenKeyExternalApp = imported.OmenKeyExternalApp;
        }

        /// <summary>
        /// Reset configuration to defaults (creates backup first)
        /// </summary>
        public async Task<bool> ResetToDefaultsAsync()
        {
            try
            {
                var result = System.Windows.MessageBox.Show(
                    "This will reset ALL OmenCore settings to defaults.\n\n" +
                    "A backup of your current configuration will be created first.\n\n" +
                    "Continue?",
                    "Reset Configuration",
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes)
                    return false;

                // Create backup first
                var backupPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OmenCore",
                    $"config-backup-before-reset-{DateTime.Now:yyyyMMdd-HHmmss}.json");

                var backup = new ConfigBackup
                {
                    ExportDate = DateTime.Now,
                    Version = GetAppVersion(),
                    Config = _configService.Config
                };

                var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(backupPath, json);

                _logging.Info($"Created pre-reset backup: {backupPath}");

                // Reset to defaults
                _configService.ResetToDefaults();
                _configService.Save(_configService.Config);

                _logging.Info("Configuration reset to defaults");

                System.Windows.MessageBox.Show(
                    $"Configuration reset to defaults!\n\n" +
                    $"Backup saved to:\n{backupPath}\n\n" +
                    "Please restart OmenCore for changes to take effect.",
                    "Reset Complete",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);

                return true;
            }
            catch (Exception ex)
            {
                _logging.Error($"Failed to reset configuration: {ex.Message}", ex);
                System.Windows.MessageBox.Show(
                    $"Failed to reset configuration:\n{ex.Message}",
                    "Reset Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
                return false;
            }
        }

        private string GetAppVersion()
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            return version?.ToString() ?? "1.0.0";
        }
    }

    /// <summary>
    /// Container for configuration backup
    /// </summary>
    public class ConfigBackup
    {
        public DateTime ExportDate { get; set; }
        public string Version { get; set; } = string.Empty;
        public AppConfig Config { get; set; } = new();
    }
}
