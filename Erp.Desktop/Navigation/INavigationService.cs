using System.ComponentModel;

namespace Erp.Desktop.Navigation;

public interface INavigationService : INotifyPropertyChanged
{
    object? CurrentViewModel { get; }
    bool NavigateTo<TViewModel>() where TViewModel : class;
}
