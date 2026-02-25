using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Desktop.Navigation;

namespace Erp.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _nav;

    public MainWindowViewModel(INavigationService nav)
    {
        _nav = nav;

        // default route
        _nav.NavigateTo<HomeViewModel>();
    }

    public object? CurrentViewModel => _nav.CurrentViewModel;

    [RelayCommand]
    private void GoHome()
    {
        _nav.NavigateTo<HomeViewModel>();
        OnPropertyChanged(nameof(CurrentViewModel));
    }
}
