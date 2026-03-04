using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Erp.Desktop.Controls;

public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status),
        typeof(string),
        typeof(StatusBadge),
        new PropertyMetadata(string.Empty, OnVisualPropertyChanged));

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(StatusBadge),
        new PropertyMetadata(null, OnVisualPropertyChanged));

    public StatusBadge()
    {
        InitializeComponent();
        Loaded += (_, _) => ApplyVisual();
    }

    public string? Status
    {
        get => (string?)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string? Text
    {
        get => (string?)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string DisplayText => string.IsNullOrWhiteSpace(Text) ? (Status ?? "Unknown") : Text!;

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusBadge badge)
        {
            badge.ApplyVisual();
        }
    }

    private void ApplyVisual()
    {
        var status = (Status ?? string.Empty).Trim().ToLowerInvariant();

        var background = Color.FromRgb(0xE7, 0xF6, 0xEC);
        var border = Color.FromRgb(0x98, 0xD8, 0xAC);
        var foreground = Color.FromRgb(0x06, 0x76, 0x47);

        switch (status)
        {
            case "pending":
                background = Color.FromRgb(0xFF, 0xF7, 0xE6);
                border = Color.FromRgb(0xFD, 0xC9, 0x7C);
                foreground = Color.FromRgb(0xB5, 0x47, 0x08);
                break;
            case "disabled":
                background = Color.FromRgb(0xFD, 0xEC, 0xEA);
                border = Color.FromRgb(0xF5, 0xB5, 0xAD);
                foreground = Color.FromRgb(0xB4, 0x23, 0x18);
                break;
            case "rejected":
                background = Color.FromRgb(0xF4, 0xF4, 0xF5);
                border = Color.FromRgb(0xD1, 0xD5, 0xDB);
                foreground = Color.FromRgb(0x47, 0x55, 0x69);
                break;
            case "active":
                break;
            default:
                background = Color.FromRgb(0xEF, 0xF2, 0xF6);
                border = Color.FromRgb(0xD0, 0xD7, 0xE2);
                foreground = Color.FromRgb(0x47, 0x55, 0x69);
                break;
        }

        BadgeBorder.Background = new SolidColorBrush(background);
        BadgeBorder.BorderBrush = new SolidColorBrush(border);
        BadgeText.Foreground = new SolidColorBrush(foreground);
        BadgeText.Text = DisplayText;
    }
}
