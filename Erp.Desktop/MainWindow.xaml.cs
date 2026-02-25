using System.Windows;
using Erp.Desktop.ViewModels;

namespace Erp.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
