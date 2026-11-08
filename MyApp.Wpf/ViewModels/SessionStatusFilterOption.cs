using MyApp.Domain.Enums;

namespace MyApp.Wpf.ViewModels;

public sealed record SessionStatusFilterOption(string Label, SessionStatus? Value);