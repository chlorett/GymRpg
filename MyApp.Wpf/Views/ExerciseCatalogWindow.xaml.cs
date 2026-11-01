using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class ExerciseCatalogWindow : Window
{
    private readonly ExerciseCatalogViewModel viewModel;

    public ExerciseCatalogWindow(ExerciseCatalogViewModel viewModel)
    {
        this.InitializeComponent();
        this.viewModel = viewModel;
        this.DataContext = viewModel;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) => this.viewModel.RefreshCommand.Execute(null);
}