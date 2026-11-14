using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class PersonalRecordsWindow : Window
{
    private readonly PersonalRecordsViewModel _viewModel;

    public PersonalRecordsWindow(PersonalRecordsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
        Loaded += async (_, _) => await _viewModel.LoadOverviewAsync();
    }
}
