using CommunityToolkit.Mvvm.ComponentModel;
using Erp.Application.Authorization;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

public abstract partial class PlaceholderPageViewModel : ObservableObject
{
    protected PlaceholderPageViewModel(string title, string description)
    {
        Title = title;
        Description = description;
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private string description = string.Empty;
}

[RequiredPermission(PermissionCodes.MasterItemsRead)]
public sealed class WarehousesViewModel : PlaceholderPageViewModel
{
    public WarehousesViewModel() : base("창고 관리", "창고 기준정보 관리 화면(placeholder)")
    {
    }
}

