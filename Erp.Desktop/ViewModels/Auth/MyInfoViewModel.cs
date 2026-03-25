using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

public sealed partial class MyInfoViewModel : ObservableObject
{
    private readonly ICurrentUserContext _currentUserContext;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private string name = "-";

    [ObservableProperty]
    private string company = "미등록";

    [ObservableProperty]
    private string phoneNumber = "미등록";

    [ObservableProperty]
    private string email = "미등록";

    [ObservableProperty]
    private string position = "미등록";

    [ObservableProperty]
    private string note = "회사/전화번호/직급은 아직 계정 정보 연동 전이라 임시 표시됩니다.";

    public MyInfoViewModel(ICurrentUserContext currentUserContext, INavigationService navigationService)
    {
        _currentUserContext = currentUserContext;
        _navigationService = navigationService;

        _currentUserContext.Changed += OnCurrentUserChanged;
        Refresh();
    }

    [RelayCommand]
    private void OpenChangePassword()
    {
        _navigationService.NavigateTo<ChangePasswordViewModel>();
    }

    private void OnCurrentUserChanged(object? sender, EventArgs e)
    {
        Refresh();
    }

    private void Refresh()
    {
        Name = string.IsNullOrWhiteSpace(_currentUserContext.Name)
            ? (_currentUserContext.Username ?? "-")
            : _currentUserContext.Name!;

        Email = string.IsNullOrWhiteSpace(_currentUserContext.Email)
            ? "xhbtsupport@gmail.com"
            : _currentUserContext.Email!;
        Company = string.IsNullOrWhiteSpace(_currentUserContext.Company)
            ? "OO회사"
            : _currentUserContext.Company!;
        PhoneNumber = string.IsNullOrWhiteSpace(_currentUserContext.PhoneNumber)
            ? "010-0000-0000"
            : _currentUserContext.PhoneNumber!;
        Position = ResolvePosition();

        Note = string.IsNullOrWhiteSpace(_currentUserContext.Company) ||
               string.IsNullOrWhiteSpace(_currentUserContext.PhoneNumber)
            ? "회사/전화번호 정보는 가입 시 입력하거나 추후 연동 후 표시됩니다."
            : "가입 시 입력한 내 정보입니다.";
    }

    private string ResolvePosition()
    {
        if (!_currentUserContext.IsAuthenticated)
        {
            return "Guest";
        }

        if (_currentUserContext.HasPermission(PermissionCodes.MasterUsersWrite) ||
            _currentUserContext.HasPermission(PermissionCodes.SystemSettingsWrite))
        {
            return "Admin";
        }

        return "Staff";
    }
}
