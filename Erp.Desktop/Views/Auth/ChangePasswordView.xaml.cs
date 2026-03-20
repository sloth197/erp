using System.Windows.Controls;
using Erp.Desktop.ViewModels;

namespace Erp.Desktop.Views;

public partial class ChangePasswordView : UserControl
{
    public ChangePasswordView()
    {
        InitializeComponent();
    }

    private void CurrentPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox box)
        {
            vm.CurrentPassword = box.Password;
        }
    }

    private void NewPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox box)
        {
            vm.NewPassword = box.Password;
        }
    }

    private void ConfirmPasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ChangePasswordViewModel vm && sender is PasswordBox box)
        {
            vm.ConfirmPassword = box.Password;
        }
    }
}
