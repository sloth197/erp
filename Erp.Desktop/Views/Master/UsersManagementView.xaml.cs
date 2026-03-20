using System.Windows.Controls;
using Erp.Desktop.ViewModels;

namespace Erp.Desktop.Views;

public partial class UsersManagementView : UserControl
{
    public UsersManagementView()
    {
        InitializeComponent();
    }

    private void CreatePasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UsersManagementViewModel vm && sender is PasswordBox box)
        {
            vm.NewPassword = box.Password;
        }
    }
}
