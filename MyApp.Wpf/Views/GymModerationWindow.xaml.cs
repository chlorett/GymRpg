using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views
{
    public partial class GymModerationWindow : Window
    {
        private readonly GymModerationViewModel _viewModel;

        public GymModerationWindow(GymModerationViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            Loaded += async (_, _) => await _viewModel.LoadRosterAsync();
        }
    }
}
