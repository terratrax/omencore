using System;
using FluentAssertions;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class TelemetryServiceTests : IDisposable
    {
        private readonly string _tempDir;

        public TelemetryServiceTests()
        {
            _tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "omen_test_config_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_tempDir);
            Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", _tempDir);
        }

        public void Dispose()
        {
            try
            {
                Environment.SetEnvironmentVariable("OMENCORE_CONFIG_DIR", null);
                if (System.IO.Directory.Exists(_tempDir)) System.IO.Directory.Delete(_tempDir, true);
            }
            catch { }
        }

        [Fact]
        public void IncrementPidStats_RespectsOptIn()
        {
            var log = new LoggingService();
            var cfg = new ConfigurationService();
            var telemetry = new TelemetryService(log, cfg);

            // By default telemetry disabled
            cfg.Config.TelemetryEnabled.Should().BeFalse();

            telemetry.IncrementPidSuccess(0x1B2E);
            telemetry.GetStats().Should().BeEmpty();

            // Enable telemetry and increment
            cfg.Config.TelemetryEnabled = true;
            telemetry.IncrementPidSuccess(0x1B2E);
            var stats = telemetry.GetStats();
            stats.Should().ContainKey("6966"); // decimal of 0x1B2E
            stats["6966"].Success.Should().BeGreaterThan(0);

            telemetry.IncrementPidFailure(0x1B2E);
            stats = telemetry.GetStats();
            stats["6966"].Failure.Should().BeGreaterThan(0);
        }
    }
}
