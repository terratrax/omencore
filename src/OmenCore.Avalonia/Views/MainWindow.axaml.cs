using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace OmenCore.Avalonia.Views;

public partial class MainWindow : Window
{
    public static FuncValueConverter<bool, Color> BoolToColorConverter { get; } = 
        new(value => value ? Color.Parse("#39FF14") : Color.Parse("#E31837"));

    public MainWindow()
    {
        InitializeComponent();
    }
}
