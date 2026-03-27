using System.Collections.ObjectModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Desktop.Services;

namespace Erp.Desktop.Views;

public partial class AddressSearchWindow : Window
{
    private const string ConfirmKey = "TESTJUSOGOKR";
    private const string ApiUrl = "https://business.juso.go.kr/addrlink/addrLinkApi.do";
    private static readonly HttpClient HttpClient = new();
    private readonly AddressSearchWindowViewModel _viewModel;

    public AddressSearchWindow(string? initialKeyword)
    {
        InitializeComponent();

        _viewModel = new AddressSearchWindowViewModel(initialKeyword, SearchAsync);
        DataContext = _viewModel;
        Loaded += OnLoaded;
    }

    public AddressSearchResult? SelectedResult { get; private set; }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.Keyword))
        {
            await _viewModel.SearchAsync();
        }

        KeywordTextBox.Focus();
        KeywordTextBox.SelectAll();
    }

    private async Task<IReadOnlyList<AddressSearchResult>> SearchAsync(string keyword)
    {
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var query = $"{ApiUrl}?currentPage=1&countPerPage=30&keyword={encodedKeyword}&confmKey={ConfirmKey}&resultType=json";
        var response = await HttpClient.GetFromJsonAsync<JusoApiResponse>(query);

        var code = response?.Results?.Common?.ErrorCode;
        if (code is not "0")
        {
            var message = response?.Results?.Common?.ErrorMessage ?? "주소 검색에 실패했습니다.";
            throw new InvalidOperationException(message);
        }

        return response?.Results?.Juso?
            .Select(x => new AddressSearchResult(
                x.ZipNo ?? string.Empty,
                x.RoadAddr ?? string.Empty,
                x.JibunAddr ?? string.Empty))
            .ToList() ?? [];
    }

    private void SelectButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not AddressSearchResult row)
        {
            return;
        }

        SelectedResult = row;
        DialogResult = true;
        Close();
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private sealed partial class AddressSearchWindowViewModel : ObservableObject
    {
        private readonly Func<string, Task<IReadOnlyList<AddressSearchResult>>> _searchFunc;

        public AddressSearchWindowViewModel(
            string? initialKeyword,
            Func<string, Task<IReadOnlyList<AddressSearchResult>>> searchFunc)
        {
            _searchFunc = searchFunc;
            keyword = initialKeyword ?? string.Empty;
        }

        [ObservableProperty]
        private string keyword = string.Empty;

        [ObservableProperty]
        private string statusText = "검색어를 입력하고 검색 버튼을 눌러 주세요.";

        [ObservableProperty]
        private bool isBusy;

        public ObservableCollection<AddressSearchResult> Results { get; } = [];

        [RelayCommand(CanExecute = nameof(CanSearch))]
        public async Task SearchAsync()
        {
            var normalizedKeyword = Keyword?.Trim();
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                StatusText = "검색어를 입력해 주세요.";
                return;
            }

            try
            {
                IsBusy = true;
                SearchCommand.NotifyCanExecuteChanged();
                StatusText = "주소를 검색하는 중...";

                var rows = await _searchFunc(normalizedKeyword);
                Results.Clear();
                foreach (var row in rows)
                {
                    Results.Add(row);
                }

                StatusText = rows.Count == 0
                    ? "검색 결과가 없습니다."
                    : $"검색 결과 {rows.Count:N0}건";
            }
            catch (Exception ex)
            {
                Results.Clear();
                StatusText = ex.Message;
            }
            finally
            {
                IsBusy = false;
                SearchCommand.NotifyCanExecuteChanged();
            }
        }

        private bool CanSearch()
        {
            return !IsBusy;
        }
    }

    private sealed class JusoApiResponse
    {
        [JsonPropertyName("results")]
        public JusoApiResults? Results { get; set; }
    }

    private sealed class JusoApiResults
    {
        [JsonPropertyName("common")]
        public JusoApiCommon? Common { get; set; }

        [JsonPropertyName("juso")]
        public List<JusoApiRow>? Juso { get; set; }
    }

    private sealed class JusoApiCommon
    {
        [JsonPropertyName("errorCode")]
        public string? ErrorCode { get; set; }

        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
    }

    private sealed class JusoApiRow
    {
        [JsonPropertyName("zipNo")]
        public string? ZipNo { get; set; }

        [JsonPropertyName("roadAddr")]
        public string? RoadAddr { get; set; }

        [JsonPropertyName("jibunAddr")]
        public string? JibunAddr { get; set; }
    }
}
