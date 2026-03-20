using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Erp.Desktop.ViewModels;

public sealed record UserMessageModel(string Kind, string Text)
{
    public static UserMessageModel Success(string text) => new("Success", text);
    public static UserMessageModel Error(string text) => new("Error", text);
}

public abstract class ViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _busyMessage;
    private UserMessageModel? _userMessage;

    protected ViewModelBase()
    {
        ValidationErrors.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasValidationErrors));
    }

    public bool IsBusy
    {
        get => _isBusy;
        protected set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnBusyStateChanged(value);
            }
        }
    }

    public string? BusyMessage
    {
        get => _busyMessage;
        protected set => SetProperty(ref _busyMessage, value);
    }

    public UserMessageModel? UserMessage
    {
        get => _userMessage;
        protected set
        {
            if (SetProperty(ref _userMessage, value))
            {
                OnPropertyChanged(nameof(HasUserMessage));
            }
        }
    }

    public bool HasUserMessage => UserMessage is { Text.Length: > 0 };

    public ObservableCollection<string> ValidationErrors { get; } = new();

    public bool HasValidationErrors => ValidationErrors.Count > 0;

    protected virtual void OnBusyStateChanged(bool isBusy)
    {
    }

    protected void SetBusy(bool isBusy, string? message = null)
    {
        if (isBusy)
        {
            BusyMessage = string.IsNullOrWhiteSpace(message) ? "Loading..." : message;
            IsBusy = true;
            return;
        }

        IsBusy = false;
        BusyMessage = null;
    }

    protected void ClearUserMessage()
    {
        UserMessage = null;
    }

    protected void SetSuccess(string message)
    {
        UserMessage = UserMessageModel.Success(message);
    }

    protected void SetError(string message)
    {
        UserMessage = UserMessageModel.Error(message);
    }

    protected void ClearValidationErrors()
    {
        ValidationErrors.Clear();
    }

    protected void AddValidationError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ValidationErrors.Add(message);
    }
}
