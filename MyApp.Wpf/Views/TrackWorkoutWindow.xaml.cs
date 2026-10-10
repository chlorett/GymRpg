using System.Windows;
using MyApp.Wpf.ViewModels;

namespace MyApp.Wpf.Views;

public partial class TrackWorkoutWindow : Window
{
    public TrackWorkoutWindow(TrackWorkoutViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadExercisesAsync();
    }
}
