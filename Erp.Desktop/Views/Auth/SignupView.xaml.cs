using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Erp.Desktop.ViewModels;

namespace Erp.Desktop.Views;

public partial class SignupView : UserControl
{
    private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private SignupViewModel? _viewModel;

    public SignupView()
    {
        InitializeComponent();
        DataContextChanged += SignupView_OnDataContextChanged;
        Unloaded += SignupView_OnUnloaded;
        _countdownTimer.Tick += CountdownTimer_OnTick;
        tbTime.Text = "00:00";
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

        SyncCountdownState();
    }

    private void ViewModel_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (e.PropertyName == nameof(SignupViewModel.Password))
        {
            if (!string.Equals(PasswordBox.Password, _viewModel.Password, StringComparison.Ordinal))
            {
                PasswordBox.Password = _viewModel.Password;
            }

            return;
        }

        if (e.PropertyName == nameof(SignupViewModel.IsCodeSent) ||
            e.PropertyName == nameof(SignupViewModel.CodeExpiresAtUtc) ||
            e.PropertyName == nameof(SignupViewModel.IsEmailVerified))
        {
            SyncCountdownState();
        }
    }

    private void SignupView_OnUnloaded(object sender, RoutedEventArgs e)
    {
        _countdownTimer.Stop();
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= ViewModel_OnPropertyChanged;
        }
    }

    private void CountdownTimer_OnTick(object? sender, EventArgs e)
    {
        UpdateCountdownText();
    }

    private void SyncCountdownState()
    {
        if (_viewModel is null ||
            !_viewModel.IsCodeSent ||
            _viewModel.IsEmailVerified ||
            !_viewModel.CodeExpiresAtUtc.HasValue)
        {
            _countdownTimer.Stop();
            tbTime.Text = "00:00";
            return;
        }

        UpdateCountdownText();
        if (_viewModel.IsCodeSent)
        {
            _countdownTimer.Start();
        }
    }

    private void UpdateCountdownText()
    {
        if (_viewModel is null || !_viewModel.CodeExpiresAtUtc.HasValue)
        {
            _countdownTimer.Stop();
            tbTime.Text = "00:00";
            return;
        }

        var remaining = _viewModel.CodeExpiresAtUtc.Value - DateTime.UtcNow;
        if (remaining <= TimeSpan.Zero)
        {
            _countdownTimer.Stop();
            tbTime.Text = "00:00";

            if (_viewModel.IsCodeSent && !_viewModel.IsEmailVerified)
            {
                _viewModel.IsCodeSent = false;
                _viewModel.VerificationCode = string.Empty;
            }

            return;
        }

        tbTime.Text = $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }
}
