using System.Threading.Tasks;
using FluentAssertions;
using OmenCore.Services.Corsair;
using OmenCore.Services;
using Xunit;

namespace OmenCoreApp.Tests.Services
{
    public class CorsairHidPayloadTests
    {
        private class TestCorsairHidDirect : CorsairHidDirect
        {
            public TestCorsairHidDirect(LoggingService logging) : base(logging) { }

            public byte[] CallBuildSetColorReport(int pid, byte r, byte g, byte b)
            {
                // Add a test device to ensure device.ProductId is set
                AddTestHidDevice("test", pid, OmenCore.Corsair.CorsairDeviceType.Keyboard, "Test");
                var list = DiscoverDevicesAsync().Result;
                var dev = default(OmenCore.Corsair.CorsairDevice);
                foreach (var d in list) if (d.DeviceId == "test") dev = d;

                // locate internal CorsairHidDevice
                var hidDevice = GetInternalHidDevice("test");
                var method = typeof(CorsairHidDirect).GetMethod("BuildSetColorReport", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                return (byte[])method.Invoke(this, new object[] { hidDevice, r, g, b })!;
            }

            private object GetInternalHidDevice(string deviceId)
            {
                var t = typeof(CorsairHidDirect);
                var field = t.GetField("_devices", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                var list = field.GetValue(this) as System.Collections.IEnumerable;
                foreach (var item in list!)
                {
                    var di = item.GetType().GetProperty("DeviceInfo").GetValue(item) as OmenCore.Corsair.CorsairDevice;
                    if (di.DeviceId == deviceId) return item;
                }
                throw new System.Exception("internal hid device not found");
            }
        }

        [Theory]
        [InlineData(0x1B11, 0x09)] // K70
        [InlineData(0x1B2D, 0x09)] // K95
        [InlineData(0x1B60, 0x09)] // K100
        [InlineData(0x1B2E, 0x05)] // Dark Core Mouse
        [InlineData(0xFFFF, 0x07)] // Unknown product -> default
        public void BuildSetColorReport_SelectsExpectedCommand(int pid, int expectedCmd)
        {
            var log = new LoggingService();
            var t = new TestCorsairHidDirect(log);

            var report = t.CallBuildSetColorReport(pid, 0x11, 0x22, 0x33);

            report[1].Should().Be((byte)expectedCmd);
            report[4].Should().Be(0x11);
            report[5].Should().Be(0x22);
            report[6].Should().Be(0x33);
        }
    }
}
