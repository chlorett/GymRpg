using CommunityToolkit.Mvvm.ComponentModel;

namespace MyApp.Wpf.ViewModels;

public class WorkoutSetItem : ObservableObject
{
    private int _setNumber;
    private int _reps = 10;
    private decimal _weight = 50m;

    public int SetNumber
    {
        get => _setNumber;
        set => SetProperty(ref _setNumber, value);
    }

    public int Reps
    {
        get => _reps;
        set => SetProperty(ref _reps, value);
    }

    public decimal Weight
    {
        get => _weight;
        set => SetProperty(ref _weight, value);
    }
}
