using System;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using Erp.Application.Interfaces;
using Erp.Desktop.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Desktop.Navigation;

public sealed partial class NavigationService : ObservableObject, INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly IUserMessageService _messageService;

    [ObservableProperty]
    private object? currentViewModel;

    public NavigationService(
        IServiceProvider serviceProvider,
        ICurrentUserContext currentUserContext,
        IUserMessageService messageService)
    {
        _serviceProvider = serviceProvider;
        _currentUserContext = currentUserContext;
        _messageService = messageService;
    }

    public bool NavigateTo<TViewModel>() where TViewModel : class
    {
        var viewModelType = typeof(TViewModel);

        if (!CanNavigate(viewModelType))
        {
            return false;
        }

        try
        {
            CurrentViewModel = _serviceProvider.GetRequiredService<TViewModel>();
            return true;
        }
        catch (Exception ex)
        {
            _messageService.ShowError($"화면을 여는 중 오류가 발생했습니다: {ex.Message}");
            return false;
        }
    }

    private bool CanNavigate(Type viewModelType)
    {
        var allowAnonymous = viewModelType.GetCustomAttribute<AllowAnonymousNavigationAttribute>() is not null;
        if (!allowAnonymous && !_currentUserContext.IsAuthenticated)
        {
            _messageService.ShowWarning("로그인이 필요합니다.");
            return false;
        }

        var requiredPermissions = viewModelType.GetCustomAttributes<RequiredPermissionAttribute>(inherit: true);
        foreach (var required in requiredPermissions)
        {
            if (!_currentUserContext.HasPermission(required.Code))
            {
                _messageService.ShowWarning("권한이 없습니다.");
                return false;
            }
        }

        return true;
    }
}
