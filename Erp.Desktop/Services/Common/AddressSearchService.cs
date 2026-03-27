using Erp.Desktop.Views;

namespace Erp.Desktop.Services;

public sealed class AddressSearchService : IAddressSearchService
{
    public AddressSearchResult? Search(string? keyword)
    {
        var dialog = new AddressSearchWindow(keyword)
        {
            Owner = ResolveActiveWindow()
        };

        var opened = dialog.ShowDialog();
        return opened == true ? dialog.SelectedResult : null;
    }

    private static System.Windows.Window? ResolveActiveWindow()
    {
        return System.Windows.Application.Current?.Windows
            .OfType<System.Windows.Window>()
            .FirstOrDefault(x => x.IsActive);
    }
}
