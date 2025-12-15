using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OmenCore.Views
{
    /// <summary>
    /// Simplified General view with paired Performance + Fan profiles.
    /// Each profile combines a performance mode with a matching fan configuration.
    /// </summary>
    public partial class GeneralView : UserControl
    {
        public GeneralView()
        {
            InitializeComponent();
        }

        private void Profile_Performance_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.GeneralViewModel vm)
            {
                vm.ApplyPerformanceProfile();
            }
        }

        private void Profile_Balanced_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.GeneralViewModel vm)
            {
                vm.ApplyBalancedProfile();
            }
        }

        private void Profile_Quiet_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.GeneralViewModel vm)
            {
                vm.ApplyQuietProfile();
            }
        }

        private void Profile_Custom_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is ViewModels.GeneralViewModel vm)
            {
                vm.ApplyCustomProfile();
            }
        }
    }
}
