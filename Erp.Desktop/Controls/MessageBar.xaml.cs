using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Erp.Desktop.Controls;

public partial class MessageBar : UserControl
{
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(
        nameof(Message),
        typeof(string),
        typeof(MessageBar),
        new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(
        nameof(Kind),
        typeof(string),
        typeof(MessageBar),
        new PropertyMetadata("Success", OnKindChanged));

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen),
        typeof(bool),
        typeof(MessageBar),
        new PropertyMetadata(false));

    public MessageBar()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyKindVisual();
    }

    public string? Message
    {
        get => (string?)GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public string? Kind
    {
        get => (string?)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MessageBar bar)
        {
            bar.ApplyKindVisual();
        }
    }

    private void ApplyKindVisual()
    {
        var isError = string.Equals(Kind, "Error", StringComparison.OrdinalIgnoreCase);

        if (isError)
        {
            MessageBorder.Background = new SolidColorBrush(Color.FromRgb(0xFD, 0xEC, 0xEA));
            MessageBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xF5, 0xB5, 0xAD));
            KindIcon.Text = "!";
            KindIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xB4, 0x23, 0x18));
            return;
        }

        MessageBorder.Background = new SolidColorBrush(Color.FromRgb(0xE7, 0xF6, 0xEC));
        MessageBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0x98, 0xD8, 0xAC));
        KindIcon.Text = "\u2713";
        KindIcon.Foreground = new SolidColorBrush(Color.FromRgb(0x06, 0x76, 0x47));
    }
}
