using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Erp.Desktop.ViewModels;

namespace Erp.Desktop.Views;

public partial class LoginView : UserControl
{
    private bool _isPasswordVisible;
    private LoginViewModel? _viewModel;

    public LoginView()
    {
        InitializeComponent();
        DataContextChanged += LoginView_OnDataContextChanged;
    }

    private void PasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm && sender is PasswordBox passwordBox)
        {
            vm.Password = passwordBox.Password;
        }
    }

    private void TogglePasswordButton_OnClick(object sender, RoutedEventArgs e)
    {
        _isPasswordVisible = !_isPasswordVisible;
        ApplyPasswordVisibility();
    }

    private void LoginView_OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }

        _viewModel = DataContext as LoginViewModel;

        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        }

        ApplyPasswordVisibility();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LoginViewModel.Password) || _viewModel is null)
        {
            return;
        }

        if (_isPasswordVisible)
        {
            if (!string.Equals(PasswordTextBox.Text, _viewModel.Password, StringComparison.Ordinal))
            {
                PasswordTextBox.Text = _viewModel.Password;
            }

            return;
        }

        if (!string.Equals(PasswordBox.Password, _viewModel.Password, StringComparison.Ordinal))
        {
            PasswordBox.Password = _viewModel.Password;
        }
    }

    private void ApplyPasswordVisibility()
    {
        if (_isPasswordVisible)
        {
            if (DataContext is LoginViewModel vm)
            {
                PasswordTextBox.Text = vm.Password;
            }

            PasswordTextBox.Visibility = Visibility.Visible;
            PasswordBox.Visibility = Visibility.Collapsed;
            TogglePasswordButton.Content = "숨김";

            PasswordTextBox.Focus();
            PasswordTextBox.SelectionStart = PasswordTextBox.Text.Length;
            return;
        }

        if (DataContext is LoginViewModel model)
        {
            model.Password = PasswordTextBox.Text;
            PasswordBox.Password = model.Password;
        }
        else
        {
            PasswordBox.Password = PasswordTextBox.Text;
        }

        PasswordTextBox.Visibility = Visibility.Collapsed;
        PasswordBox.Visibility = Visibility.Visible;
        TogglePasswordButton.Content = "보기";

        PasswordBox.Focus();
    }
}
