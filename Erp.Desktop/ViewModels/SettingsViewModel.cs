using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Exceptions;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.SystemSettingsRead)]
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IAccessControl _accessControl;

    [ObservableProperty]
    private bool enableDetailedErrors = true;

    [ObservableProperty]
    private string? statusMessage;

    public SettingsViewModel(IAccessControl accessControl)
    {
        _accessControl = accessControl;
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _accessControl.DemandPermission(PermissionCodes.SystemSettingsWrite);
            StatusMessage = "설정이 저장되었습니다. (MVP 샘플)";
        }
        catch (ForbiddenException)
        {
            StatusMessage = "설정을 저장할 권한이 없습니다.";
        }
    }
}
