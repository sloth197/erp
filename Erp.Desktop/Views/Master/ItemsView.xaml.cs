using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using Erp.Desktop.ViewModels;

namespace Erp.Desktop.Views;

public partial class ItemsView : UserControl
{
    public ItemsView()
    {
        InitializeComponent();
    }

    private void ItemsGrid_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ItemsViewModel vm || sender is not DataGrid grid)
        {
            return;
        }

        if (grid.SelectedItem is ItemsViewModel.ItemRow row)
        {
            vm.SelectItemFromGridCommand.Execute(row);
        }
    }

    private async void ItemsGrid_OnSorting(object sender, DataGridSortingEventArgs e)
    {
        if (DataContext is not ItemsViewModel vm)
        {
            return;
        }

        e.Handled = true;

        var direction = e.Column.SortDirection == ListSortDirection.Ascending
            ? ListSortDirection.Descending
            : ListSortDirection.Ascending;

        await vm.ApplyGridSortAsync(e.Column.SortMemberPath, direction);

        if (sender is DataGrid grid)
        {
            foreach (var column in grid.Columns)
            {
                if (!ReferenceEquals(column, e.Column))
                {
                    column.SortDirection = null;
                }
            }
        }

        e.Column.SortDirection = direction;
    }

    private void ItemsView_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ItemsViewModel vm || Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.F:
                KeywordSearchTextBox.Focus();
                KeywordSearchTextBox.SelectAll();
                e.Handled = true;
                break;
            case Key.N when vm.AddItemCommand.CanExecute(null):
                vm.AddItemCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.S when vm.SaveItemCommand.CanExecute(null):
                vm.SaveItemCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
