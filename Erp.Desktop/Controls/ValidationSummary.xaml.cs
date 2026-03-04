using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace Erp.Desktop.Controls;

public partial class ValidationSummary : UserControl
{
    public static readonly DependencyProperty ErrorsProperty = DependencyProperty.Register(
        nameof(Errors),
        typeof(IEnumerable),
        typeof(ValidationSummary),
        new PropertyMetadata(null));

    public static readonly DependencyProperty HasErrorsProperty = DependencyProperty.Register(
        nameof(HasErrors),
        typeof(bool),
        typeof(ValidationSummary),
        new PropertyMetadata(false));

    public ValidationSummary()
    {
        InitializeComponent();
    }

    public IEnumerable? Errors
    {
        get => (IEnumerable?)GetValue(ErrorsProperty);
        set => SetValue(ErrorsProperty, value);
    }

    public bool HasErrors
    {
        get => (bool)GetValue(HasErrorsProperty);
        set => SetValue(HasErrorsProperty, value);
    }
}
