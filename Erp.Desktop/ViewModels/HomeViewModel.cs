using CommunityToolkit.Mvvm.ComponentModel;

namespace Erp.Desktop.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "ERP Desktop - Home";

    [ObservableProperty]
    private string subtitle = "Host/DI + MVVM + Navigation baseline is ready.";
}
