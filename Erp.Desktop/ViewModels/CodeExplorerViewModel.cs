using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Desktop.Services;

namespace Erp.Desktop.ViewModels;

public sealed partial class CodeExplorerViewModel : ObservableObject
{
    private readonly ICodeExplorerService _codeExplorerService;
    private CancellationTokenSource? _previewCts;

    [ObservableProperty]
    private ObservableCollection<CodeFileItem> allFiles = new();

    [ObservableProperty]
    private ObservableCollection<CodeFileItem> filteredFiles = new();

    [ObservableProperty]
    private CodeFileItem? selectedFile;

    [ObservableProperty]
    private string fileContent = string.Empty;

    [ObservableProperty]
    private string? filterText;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool isBusy;

    [ObservableProperty]
    private string? statusMessage;

    public CodeExplorerViewModel(ICodeExplorerService codeExplorerService)
    {
        _codeExplorerService = codeExplorerService;
        _ = RefreshAsync();
    }

    public string WorkspaceRootPath => _codeExplorerService.WorkspaceRootPath;
    public int TotalCount => AllFiles.Count;
    public int FilteredCount => FilteredFiles.Count;
    public string? SelectedFilePath => SelectedFile?.RelativePath;
    public string? SelectedFileMeta => SelectedFile is null
        ? null
        : $"{SelectedFile.Length:N0} bytes | {SelectedFile.LastWriteTimeUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}";

    partial void OnFilterTextChanged(string? value)
    {
        ApplyFilter();
    }

    partial void OnSelectedFileChanged(CodeFileItem? value)
    {
        OnPropertyChanged(nameof(SelectedFilePath));
        OnPropertyChanged(nameof(SelectedFileMeta));
        _ = LoadSelectedFileAsync(value);
    }

    partial void OnAllFilesChanged(ObservableCollection<CodeFileItem> value)
    {
        OnPropertyChanged(nameof(TotalCount));
    }

    partial void OnFilteredFilesChanged(ObservableCollection<CodeFileItem> value)
    {
        OnPropertyChanged(nameof(FilteredCount));
    }

    [RelayCommand(CanExecute = nameof(CanRefresh))]
    private async Task RefreshAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Loading code files...";

            var files = await _codeExplorerService.GetCodeFilesAsync();
            AllFiles = new ObservableCollection<CodeFileItem>(
                files.Select(x => new CodeFileItem(x.RelativePath, x.Length, x.LastWriteTimeUtc)));

            ApplyFilter();
            StatusMessage = $"Loaded {TotalCount:N0} files.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanRefresh()
    {
        return !IsBusy;
    }

    private void ApplyFilter()
    {
        var keyword = FilterText?.Trim();

        IEnumerable<CodeFileItem> files = AllFiles;
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            files = files.Where(x => x.RelativePath.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        FilteredFiles = new ObservableCollection<CodeFileItem>(
            files.OrderBy(x => x.RelativePath, StringComparer.OrdinalIgnoreCase));

        if (SelectedFile is not null && !FilteredFiles.Any(x => x.RelativePath == SelectedFile.RelativePath))
        {
            SelectedFile = null;
            FileContent = string.Empty;
        }

        if (SelectedFile is null && FilteredFiles.Count > 0)
        {
            SelectedFile = FilteredFiles[0];
        }
    }

    private async Task LoadSelectedFileAsync(CodeFileItem? item)
    {
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = new CancellationTokenSource();

        if (item is null)
        {
            FileContent = string.Empty;
            return;
        }

        try
        {
            var content = await _codeExplorerService.ReadFileAsync(item.RelativePath, _previewCts.Token);
            FileContent = content;
        }
        catch (OperationCanceledException)
        {
            // Ignore rapid selection changes.
        }
        catch (Exception ex)
        {
            FileContent = string.Empty;
            StatusMessage = ex.Message;
        }
    }

    public sealed record CodeFileItem(string RelativePath, long Length, DateTime LastWriteTimeUtc)
    {
        public string FileName => Path.GetFileName(RelativePath);
    }
}
