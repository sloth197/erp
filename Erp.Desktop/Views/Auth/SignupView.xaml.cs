using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Erp.Desktop.ViewModels;

namespace Erp.Desktop.Views;

public partial class SignupView : UserControl
{
    private SignupViewModel? _viewModel;

    public SignupView()
    {
        InitializeComponent();
        DataContextChanged += SignupView_OnDataContextChanged;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is SignupViewModel vm && sender is PasswordBox passwordBox)
        {
            vm.Password = passwordBox.Password;
        }
    }

    private void SignupView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        _viewModel = DataContext as SignupViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
            if (!string.Equals(PasswordBox.Password, _viewModel.Password, StringComparison.Ordinal))
            {
                PasswordBox.Password = _viewModel.Password;
            }
        }
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null || e.PropertyName != nameof(SignupViewModel.Password))
        {
            return;
        }

        if (!string.Equals(PasswordBox.Password, _viewModel.Password, StringComparison.Ordinal))
        {
            PasswordBox.Password = _viewModel.Password;
        }
    }
}
