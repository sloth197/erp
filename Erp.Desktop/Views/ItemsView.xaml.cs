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
}
