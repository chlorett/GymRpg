using CommunityToolkit.Mvvm.ComponentModel;

namespace MyApp.Wpf.ViewModels;

public sealed class ExecutionSetRow : ObservableObject
{
    private string _reps = string.Empty;
    private string _weight = string.Empty;

    public ExecutionSetRow(int setNumber)
    {
        SetNumber = setNumber;
    }

    public int SetNumber { get; }

    public string Reps
    {
        get => _reps;
        set => SetProperty(ref _reps, value);
    }

    public string Weight
    {
        get => _weight;
        set => SetProperty(ref _weight, value);
    }
}