using Avalonia;
using System;

namespace OmenCore.Avalonia;

/// <summary>
/// Application entry point.
/// </summary>
internal sealed class Program
{
    /// <summary>
    /// Initialization code - ensure it's called before any Avalonia functionality.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    /// <summary>
    /// Avalonia configuration - used by designer and runtime.
    /// </summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
