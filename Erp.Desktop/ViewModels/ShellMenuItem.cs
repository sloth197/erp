using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Erp.Desktop.ViewModels;

public sealed partial class ShellMenuItem : ObservableObject
{
    public ShellMenuItem(string title, Type? targetViewModelType, ICommand command)
    {
        Title = title;
        TargetViewModelType = targetViewModelType;
        Command = command;
    }

    [ObservableProperty]
    private bool isSelected;

    public string Title { get; }
    public Type? TargetViewModelType { get; }
    public ICommand Command { get; }
}
