using CommunityToolkit.Mvvm.ComponentModel;

namespace Erp.Desktop.ViewModels;

public sealed partial class HomeViewModel : ObservableObject
{
    [ObservableProperty]
    private string title = "ERP Dashboard";

    [ObservableProperty]
    private string subtitle = "로그인 후 운영 모듈로 이동할 수 있습니다.";
}
