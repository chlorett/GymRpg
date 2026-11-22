using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MyApp.Application.Dtos;
using MyApp.Application.Rules;

namespace MyApp.Wpf.ViewModels;

public sealed class ExecutionExerciseRow
{
    public ExecutionExerciseRow(SessionExecutionExerciseDto exercise)
    {
        ArgumentNullException.ThrowIfNull(exercise);

        ExerciseId = exercise.ExerciseId;
        Name = exercise.Name;
        Technique = string.IsNullOrWhiteSpace(exercise.Technique)
            ? "Техніку виконання не вказано."
            : exercise.Technique;
        TargetText = $"План: {exercise.TargetSets} × {exercise.TargetReps}";
        AddSetCommand = new RelayCommand(AddSet);

        int initialSets = Math.Min(exercise.TargetSets, WorkoutRules.MaxSets);
        for (int i = 1; i <= initialSets; i++)
        {
            Sets.Add(new ExecutionSetRow(i));
        }
    }

    public int ExerciseId { get; }

    public string Name { get; }

    public string Technique { get; }

    public string TargetText { get; }

    public ObservableCollection<ExecutionSetRow> Sets { get; } = new();

    public IRelayCommand AddSetCommand { get; }

    private void AddSet()
    {
        if (Sets.Count < WorkoutRules.MaxSets)
        {
            Sets.Add(new ExecutionSetRow(Sets.Count + 1));
        }
    }
}