using System.Windows.Input;

namespace Erp.Desktop.ViewModels;

public sealed class ShellMenuItem
{
    public ShellMenuItem(string title, ICommand command)
    {
        Title = title;
        Command = command;
    }

    public string Title { get; }
    public ICommand Command { get; }
}
