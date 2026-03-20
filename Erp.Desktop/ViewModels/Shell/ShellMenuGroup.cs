namespace Erp.Desktop.ViewModels;

public sealed class ShellMenuGroup
{
    public ShellMenuGroup(string title, IReadOnlyList<ShellMenuItem> items)
    {
        Title = title;
        Items = items;
    }

    public string Title { get; }
    public IReadOnlyList<ShellMenuItem> Items { get; }
}
