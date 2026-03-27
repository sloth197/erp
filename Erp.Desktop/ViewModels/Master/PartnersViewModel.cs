using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Erp.Application.Authorization;
using Erp.Application.Interfaces;
using Erp.Desktop.Navigation;
using Erp.Desktop.Services;

namespace Erp.Desktop.ViewModels;

[RequiredPermission(PermissionCodes.MasterPartnersRead)]
public sealed partial class PartnersViewModel : ViewModelBase
{
    private readonly IAddressSearchService _addressSearchService;
    private readonly Dictionary<string, PartnerEditorSnapshot> partnerProfiles = new(StringComparer.OrdinalIgnoreCase);
    private string? editingPartnerCode;

    public PartnersViewModel(
        ICurrentUserContext currentUserContext,
        IAddressSearchService addressSearchService)
    {
        _addressSearchService = addressSearchService;
        CanRead = currentUserContext.HasPermission(PermissionCodes.MasterPartnersRead);
        CanWrite = currentUserContext.HasPermission(PermissionCodes.MasterPartnersWrite);

        PartnerKinds = new ObservableCollection<PartnerKindOption>
        {
            new("corporation", "법인"),
            new("individual", "개인"),
            new("overseas", "해외")
        };

        Countries = BuildCountryOptions();
        BankScopes = new ObservableCollection<BankScopeOption>
        {
            new("domestic", "국내"),
            new("overseas", "국외")
        };

        DomesticBanks = new ObservableCollection<BankOption>
        {
            new("KB", "KB국민은행"),
            new("SHINHAN", "신한은행"),
            new("WOORI", "우리은행"),
            new("HANA", "하나은행"),
            new("NH", "NH농협은행"),
            new("IBK", "IBK기업은행"),
            new("KAKAO", "카카오뱅크"),
            new("TOSS", "토스뱅크")
        };

        OverseasBanks = new ObservableCollection<BankOption>
        {
            new("CITI", "Citibank"),
            new("HSBC", "HSBC"),
            new("JPM", "JPMorgan Chase"),
            new("BOFA", "Bank of America"),
            new("DBS", "DBS Bank"),
            new("MIZUHO", "Mizuho Bank"),
            new("MUFG", "MUFG Bank"),
            new("BNPP", "BNP Paribas"),
            new("WELLS", "Wells Fargo")
        };

        Editor = CreateDefaultEditor();
        SeedPartners();

        if (!CanRead)
        {
            SetError("거래처 조회 권한이 없습니다.");
        }
    }

    [ObservableProperty]
    private ObservableCollection<PartnerRow> partners = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditSelectedPartnerCommand))]
    private PartnerRow? selectedPartner;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowPartnerList))]
    [NotifyPropertyChangedFor(nameof(ShowRegistrationForm))]
    [NotifyPropertyChangedFor(nameof(ActionButtonText))]
    [NotifyCanExecuteChangedFor(nameof(RegisterPartnerCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelRegistrationCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditSelectedPartnerCommand))]
    [NotifyCanExecuteChangedFor(nameof(SearchPostalCodeCommand))]
    [NotifyCanExecuteChangedFor(nameof(SearchAddressCommand))]
    private bool isRegistrationMode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
    private PartnerEditor editor = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormTitle))]
    [NotifyPropertyChangedFor(nameof(SubmitButtonText))]
    [NotifyCanExecuteChangedFor(nameof(RegisterPartnerCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelRegistrationCommand))]
    private bool isEditMode;

    public ObservableCollection<PartnerKindOption> PartnerKinds { get; }

    public ObservableCollection<CountryOption> Countries { get; }

    public ObservableCollection<BankScopeOption> BankScopes { get; }

    public ObservableCollection<BankOption> DomesticBanks { get; }

    public ObservableCollection<BankOption> OverseasBanks { get; }

    public bool CanRead { get; }

    public bool CanWrite { get; }

    public bool ShowPartnerList => !IsRegistrationMode;

    public bool ShowRegistrationForm => IsRegistrationMode;

    public string ActionButtonText => IsRegistrationMode ? "목록 보기" : "신규 등록";

    public string FormTitle => IsEditMode ? "거래처 정보 수정" : "거래처 신규 등록";

    public string SubmitButtonText => IsEditMode ? "저장" : "등록";

    public bool IsAddressSearchEnabled => true;

    [RelayCommand(CanExecute = nameof(CanToggleRegistrationMode))]
    private void ToggleRegistrationMode()
    {
        if (!CanWrite)
        {
            SetError("거래처 등록 권한이 없습니다.");
            return;
        }

        ClearUserMessage();
        ClearValidationErrors();

        if (IsRegistrationMode)
        {
            if (IsEditMode && !ConfirmDiscardEdit())
            {
                return;
            }

            IsEditMode = false;
            editingPartnerCode = null;
            IsRegistrationMode = false;
            return;
        }

        Editor = CreateDefaultEditor();
        IsEditMode = false;
        editingPartnerCode = null;
        IsRegistrationMode = true;
    }

    [RelayCommand(CanExecute = nameof(CanEditSelectedPartner))]
    private void EditSelectedPartner()
    {
        if (!CanWrite)
        {
            return;
        }

        var target = SelectedPartner ?? Partners.FirstOrDefault();
        if (target is null)
        {
            SetError("수정할 거래처를 먼저 선택해 주세요.");
            return;
        }

        ClearUserMessage();
        ClearValidationErrors();

        Editor = CreateEditorFromRow(target);
        editingPartnerCode = target.Code;
        IsEditMode = true;
        IsRegistrationMode = true;
    }

    [RelayCommand(CanExecute = nameof(CanCancelRegistration))]
    private void CancelRegistration()
    {
        if (IsEditMode && !ConfirmDiscardEdit())
        {
            return;
        }

        ClearUserMessage();
        ClearValidationErrors();
        IsEditMode = false;
        editingPartnerCode = null;
        IsRegistrationMode = false;
    }

    [RelayCommand(CanExecute = nameof(CanSearchAddress))]
    private void SearchPostalCode()
    {
        ApplyAddressSearchResult(Editor.PostalCode);
    }

    [RelayCommand(CanExecute = nameof(CanSearchAddress))]
    private void SearchAddress()
    {
        var keyword = string.IsNullOrWhiteSpace(Editor.Address) ? Editor.DetailAddress : Editor.Address;
        ApplyAddressSearchResult(keyword);
    }

    [RelayCommand(CanExecute = nameof(CanRegisterPartner))]
    private void RegisterPartner()
    {
        if (!CanWrite)
        {
            SetError("거래처 등록 권한이 없습니다.");
            return;
        }

        ClearUserMessage();
        ClearValidationErrors();

        if (!ValidateEditor())
        {
            SetError("입력 값을 확인해 주세요.");
            return;
        }

        var normalizedCode = Editor.Code.Trim().ToUpperInvariant();
        var originalCode = IsEditMode ? editingPartnerCode : null;
        var hasDuplicateCode = Partners.Any(x =>
            string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(x.Code, originalCode, StringComparison.OrdinalIgnoreCase));

        if (hasDuplicateCode)
        {
            AddValidationError("이미 사용 중인 코드입니다.");
            SetError("입력 값을 확인해 주세요.");
            return;
        }

        if (IsEditMode)
        {
            if (!ConfirmSaveEdit())
            {
                return;
            }

            var targetCode = originalCode ?? SelectedPartner?.Code;
            var targetIndex = FindPartnerIndexByCode(targetCode);
            var preservedIsActive = targetIndex >= 0 ? Partners[targetIndex].IsActive : true;
            var updatedRow = BuildPartnerRowFromEditor(preservedIsActive);
            var profile = CaptureProfileFromEditor();

            if (targetIndex >= 0)
            {
                Partners[targetIndex] = updatedRow;
            }
            else
            {
                Partners.Insert(0, updatedRow);
            }

            if (!string.IsNullOrWhiteSpace(targetCode) &&
                !string.Equals(targetCode, updatedRow.Code, StringComparison.OrdinalIgnoreCase))
            {
                partnerProfiles.Remove(targetCode);
            }

            partnerProfiles[updatedRow.Code] = profile;

            SelectedPartner = updatedRow;
            IsEditMode = false;
            editingPartnerCode = null;
            IsRegistrationMode = false;
            SetSuccess("거래처 정보가 수정되었습니다.");
            return;
        }

        var createdRow = BuildPartnerRowFromEditor(true);
        partnerProfiles[createdRow.Code] = CaptureProfileFromEditor();
        Partners.Insert(0, createdRow);
        SelectedPartner = createdRow;
        IsEditMode = false;
        editingPartnerCode = null;
        IsRegistrationMode = false;

        SetSuccess("거래처가 등록되었습니다.");
    }

    private bool CanToggleRegistrationMode()
    {
        return CanRead && CanWrite;
    }

    private bool CanEditSelectedPartner()
    {
        return CanRead && CanWrite && !IsRegistrationMode && Partners.Count > 0;
    }

    private bool CanCancelRegistration()
    {
        return IsRegistrationMode && CanWrite;
    }

    private bool CanSearchAddress()
    {
        return IsRegistrationMode && IsAddressSearchEnabled;
    }

    private bool CanRegisterPartner()
    {
        return IsRegistrationMode && CanWrite;
    }

    private bool ValidateEditor()
    {
        if (string.IsNullOrWhiteSpace(Editor.Code))
        {
            AddValidationError("코드는 필수입니다.");
        }
        else if (Editor.Code.Trim().Length > 30)
        {
            AddValidationError("코드는 30자 이하여야 합니다.");
        }

        if (Editor.SelectedPartnerKind is null)
        {
            AddValidationError("거래처 종류는 필수입니다.");
        }

        var isIndividual = string.Equals(Editor.SelectedPartnerKind?.Value, "individual", StringComparison.OrdinalIgnoreCase);
        if (isIndividual)
        {
            if (string.IsNullOrWhiteSpace(Editor.IndividualName))
            {
                AddValidationError("개인 성명은 필수입니다.");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(Editor.CompanyName))
            {
                AddValidationError("회사명은 필수입니다.");
            }

            if (string.IsNullOrWhiteSpace(Editor.BusinessRegistrationNumber))
            {
                AddValidationError("사업자등록번호는 필수입니다.");
            }

            if (string.IsNullOrWhiteSpace(Editor.RepresentativeName))
            {
                AddValidationError("대표자 성명은 필수입니다.");
            }

            if (string.IsNullOrWhiteSpace(Editor.BusinessType))
            {
                AddValidationError("업태는 필수입니다.");
            }

            if (string.IsNullOrWhiteSpace(Editor.BusinessCategory))
            {
                AddValidationError("업종은 필수입니다.");
            }
        }

        if (!string.IsNullOrWhiteSpace(Editor.BusinessRegistrationNumber) &&
            Editor.BusinessRegistrationNumber.Trim().Length > 20)
        {
            AddValidationError("사업자등록번호는 20자 이하여야 합니다.");
        }

        if (Editor.SelectedCountry is null)
        {
            AddValidationError("국가는 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(Editor.PostalCode))
        {
            AddValidationError("우편번호는 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(Editor.Address))
        {
            AddValidationError("주소는 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(Editor.DetailAddress))
        {
            AddValidationError("상세주소는 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(Editor.PhoneNumber))
        {
            AddValidationError("전화번호는 필수입니다.");
        }

        if (!string.IsNullOrWhiteSpace(Editor.Email) &&
            !Editor.Email.Contains('@', StringComparison.Ordinal))
        {
            AddValidationError("이메일 형식이 올바르지 않습니다.");
        }

        if (Editor.SelectedBankScope is null)
        {
            AddValidationError("은행 구분은 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(ResolveBankName(Editor)))
        {
            AddValidationError("은행 선택은 필수입니다.");
        }

        if (string.IsNullOrWhiteSpace(Editor.AccountNumber))
        {
            AddValidationError("계좌번호는 필수입니다.");
        }

        return !HasValidationErrors;
    }

    private void SeedPartners()
    {
        if (!CanRead)
        {
            Partners = new ObservableCollection<PartnerRow>();
            return;
        }

        Partners = new ObservableCollection<PartnerRow>
        {
            new("PT-0001", "OO유통", "법인", "공급", "김OO", "02-1234-5678", true),
            new("PT-0002", "OOO상사", "법인", "매출, 공급", "박OOO", "031-234-5678", true),
            new("PT-0003", "OXOX트레이딩", "해외", "공급", "최OO", "+1-111-222-3333", true),
            new("PT-0004", "김개발", "개인", "매출", "김개발", "010-3456-7890", true),
            new("PT-0005", "이변수", "개인", "미지급금", "이변수", "011-987-6543", false)
        };

        SelectedPartner = Partners.FirstOrDefault();
    }

    private PartnerEditor CreateDefaultEditor()
    {
        return new PartnerEditor
        {
            Code = BuildDefaultCodeValue(),
            SelectedPartnerKind = PartnerKinds.FirstOrDefault(),
            SelectedCountry = Countries.FirstOrDefault(x => x.Code == "KR") ?? Countries.FirstOrDefault(),
            SelectedBankScope = BankScopes.FirstOrDefault(x => x.Value == "domestic") ?? BankScopes.FirstOrDefault(),
            SelectedDomesticBank = DomesticBanks.FirstOrDefault(),
            IsSales = true
        };
    }

    private string BuildDefaultCodeValue()
    {
        var sourceCode = SelectedPartner?.Code ?? Partners.FirstOrDefault()?.Code;
        if (string.IsNullOrWhiteSpace(sourceCode))
        {
            return "PT-";
        }

        var codeWithoutDigits = new string(sourceCode.Where(ch => !char.IsDigit(ch)).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(codeWithoutDigits) ? "PT-" : codeWithoutDigits;
    }

    private PartnerEditor CreateEditorFromRow(PartnerRow row)
    {
        if (partnerProfiles.TryGetValue(row.Code, out var profile))
        {
            return CreateEditorFromProfile(profile);
        }

        var editor = CreateDefaultEditor();
        var partnerKind = PartnerKinds.FirstOrDefault(x => string.Equals(x.DisplayName, row.PartnerKind, StringComparison.Ordinal));

        editor.Code = row.Code;
        editor.SelectedPartnerKind = partnerKind ?? editor.SelectedPartnerKind;
        editor.CompanyName = string.Equals(editor.SelectedPartnerKind?.Value, "individual", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : row.Name;
        editor.IndividualName = string.Equals(editor.SelectedPartnerKind?.Value, "individual", StringComparison.OrdinalIgnoreCase)
            ? row.Name
            : string.Empty;
        editor.RepresentativeName = row.Representative;
        editor.PhoneNumber = row.PhoneNumber;

        editor.IsSales = row.TransactionTypes.Contains("매출", StringComparison.Ordinal);
        editor.IsSupply = row.TransactionTypes.Contains("공급", StringComparison.Ordinal);
        editor.IsReceivable = row.TransactionTypes.Contains("매수금", StringComparison.Ordinal);
        editor.IsPayable = row.TransactionTypes.Contains("미지급금", StringComparison.Ordinal);
        editor.IsEtc = row.TransactionTypes.Contains("기타", StringComparison.Ordinal);

        return editor;
    }

    private PartnerEditor CreateEditorFromProfile(PartnerEditorSnapshot profile)
    {
        var editor = CreateDefaultEditor();

        editor.Code = profile.Code;
        editor.CompanyName = profile.CompanyName;
        editor.SelectedPartnerKind = PartnerKinds.FirstOrDefault(x => string.Equals(x.Value, profile.PartnerKindValue, StringComparison.OrdinalIgnoreCase))
            ?? editor.SelectedPartnerKind;
        editor.IsSales = profile.IsSales;
        editor.IsSupply = profile.IsSupply;
        editor.IsReceivable = profile.IsReceivable;
        editor.IsPayable = profile.IsPayable;
        editor.IsEtc = profile.IsEtc;
        editor.BusinessRegistrationNumber = profile.BusinessRegistrationNumber;
        editor.IndividualName = profile.IndividualName;
        editor.ResidentNumber = profile.ResidentNumber;
        editor.RepresentativeName = profile.RepresentativeName;
        editor.BusinessType = profile.BusinessType;
        editor.BusinessCategory = profile.BusinessCategory;
        editor.SelectedCountry = Countries.FirstOrDefault(x => string.Equals(x.Code, profile.CountryCode, StringComparison.OrdinalIgnoreCase))
            ?? editor.SelectedCountry;
        editor.PostalCode = profile.PostalCode;
        editor.Address = profile.Address;
        editor.DetailAddress = profile.DetailAddress;
        editor.PhoneNumber = profile.PhoneNumber;
        editor.MobileNumber = profile.MobileNumber;
        editor.FaxNumber = profile.FaxNumber;
        editor.Email = profile.Email;
        editor.SelectedBankScope = BankScopes.FirstOrDefault(x => string.Equals(x.Value, profile.BankScopeValue, StringComparison.OrdinalIgnoreCase))
            ?? editor.SelectedBankScope;
        editor.SelectedDomesticBank = DomesticBanks.FirstOrDefault(x => string.Equals(x.Code, profile.DomesticBankCode, StringComparison.OrdinalIgnoreCase));
        editor.SelectedOverseasBank = OverseasBanks.FirstOrDefault(x => string.Equals(x.Code, profile.OverseasBankCode, StringComparison.OrdinalIgnoreCase));
        editor.AccountNumber = profile.AccountNumber;

        return editor;
    }

    private PartnerEditorSnapshot CaptureProfileFromEditor()
    {
        return new PartnerEditorSnapshot(
            Code: Editor.Code.Trim().ToUpperInvariant(),
            CompanyName: Editor.CompanyName.Trim(),
            PartnerKindValue: Editor.SelectedPartnerKind?.Value,
            IsSales: Editor.IsSales,
            IsSupply: Editor.IsSupply,
            IsReceivable: Editor.IsReceivable,
            IsPayable: Editor.IsPayable,
            IsEtc: Editor.IsEtc,
            BusinessRegistrationNumber: Editor.BusinessRegistrationNumber.Trim(),
            IndividualName: Editor.IndividualName.Trim(),
            ResidentNumber: Editor.ResidentNumber.Trim(),
            RepresentativeName: Editor.RepresentativeName.Trim(),
            BusinessType: Editor.BusinessType.Trim(),
            BusinessCategory: Editor.BusinessCategory.Trim(),
            CountryCode: Editor.SelectedCountry?.Code,
            PostalCode: Editor.PostalCode.Trim(),
            Address: Editor.Address.Trim(),
            DetailAddress: Editor.DetailAddress.Trim(),
            PhoneNumber: Editor.PhoneNumber.Trim(),
            MobileNumber: Editor.MobileNumber.Trim(),
            FaxNumber: Editor.FaxNumber.Trim(),
            Email: Editor.Email.Trim(),
            BankScopeValue: Editor.SelectedBankScope?.Value,
            DomesticBankCode: Editor.SelectedDomesticBank?.Code,
            OverseasBankCode: Editor.SelectedOverseasBank?.Code,
            AccountNumber: Editor.AccountNumber.Trim());
    }

    private PartnerRow BuildPartnerRowFromEditor(bool isActive)
    {
        var normalizedCode = Editor.Code.Trim().ToUpperInvariant();
        var transactionTypes = BuildTransactionTypeText(Editor);
        var displayName = ResolveDisplayName(Editor);
        var contactName = string.IsNullOrWhiteSpace(Editor.RepresentativeName)
            ? "-"
            : Editor.RepresentativeName.Trim();
        var phone = ResolvePhone(Editor);

        return new PartnerRow(
            normalizedCode,
            displayName,
            Editor.SelectedPartnerKind?.DisplayName ?? "-",
            transactionTypes,
            contactName,
            phone,
            isActive);
    }

    private int FindPartnerIndexByCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return -1;
        }

        for (var index = 0; index < Partners.Count; index++)
        {
            if (string.Equals(Partners[index].Code, code, StringComparison.OrdinalIgnoreCase))
            {
                return index;
            }
        }

        return -1;
    }

    private static ObservableCollection<CountryOption> BuildCountryOptions()
    {
        var koreanComparer = StringComparer.Create(CultureInfo.GetCultureInfo("ko-KR"), ignoreCase: false);

        var items = CultureInfo
            .GetCultures(CultureTypes.SpecificCultures)
            .Select(culture =>
            {
                try
                {
                    return new RegionInfo(culture.Name);
                }
                catch
                {
                    return null;
                }
            })
            .Where(region => region is not null)
            .GroupBy(region => region!.TwoLetterISORegionName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First()!)
            .Select(region => new CountryOption(
                region.TwoLetterISORegionName,
                GetKoreanCountryName(region.TwoLetterISORegionName, region.EnglishName)))
            .OrderBy(option => option.DisplayName, koreanComparer)
            .ToList();

        return new ObservableCollection<CountryOption>(items);
    }

    private static string GetKoreanCountryName(string countryCode, string fallbackName)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo($"ko-{countryCode.ToUpperInvariant()}");
            var displayName = culture.DisplayName;

            var open = displayName.IndexOf('(');
            if (open >= 0 && displayName.EndsWith(')'))
            {
                var countryName = displayName[(open + 1)..^1].Trim();
                if (!string.IsNullOrWhiteSpace(countryName))
                {
                    return countryName;
                }
            }
        }
        catch
        {
            // Fallback below.
        }

        return fallbackName;
    }

    private static string BuildTransactionTypeText(PartnerEditor editor)
    {
        var values = new List<string>();
        if (editor.IsSales)
        {
            values.Add("매출");
        }

        if (editor.IsSupply)
        {
            values.Add("공급");
        }

        if (editor.IsReceivable)
        {
            values.Add("매수금");
        }

        if (editor.IsPayable)
        {
            values.Add("미지급금");
        }

        if (editor.IsEtc)
        {
            values.Add("기타");
        }

        return values.Count == 0 ? "일반" : string.Join(", ", values);
    }

    private static string ResolveDisplayName(PartnerEditor editor)
    {
        if (!string.IsNullOrWhiteSpace(editor.CompanyName))
        {
            return editor.CompanyName.Trim();
        }

        return editor.IndividualName.Trim();
    }

    private static string ResolvePhone(PartnerEditor editor)
    {
        if (!string.IsNullOrWhiteSpace(editor.PhoneNumber))
        {
            return editor.PhoneNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(editor.MobileNumber))
        {
            return editor.MobileNumber.Trim();
        }

        return "-";
    }

    private static string ResolveBankName(PartnerEditor editor)
    {
        var bank = string.Equals(editor.SelectedBankScope?.Value, "overseas", StringComparison.OrdinalIgnoreCase)
            ? editor.SelectedOverseasBank?.DisplayName
            : editor.SelectedDomesticBank?.DisplayName;

        if (!string.IsNullOrWhiteSpace(bank))
        {
            return bank;
        }

        return string.Empty;
    }

    private static bool ConfirmSaveEdit()
    {
        return MessageBox.Show(
            "저장하시겠습니까?",
            "확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private static bool ConfirmDiscardEdit()
    {
        return MessageBox.Show(
            "수정을 취소하시겠습니까?",
            "확인",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private void ApplyAddressSearchResult(string? keyword)
    {
        try
        {
            var result = _addressSearchService.Search(keyword);
            if (result is null)
            {
                return;
            }

            Editor.PostalCode = result.ZipNo;
            Editor.Address = result.RoadAddress;
            SetSuccess("주소가 적용되었습니다.");
        }
        catch (Exception ex)
        {
            SetError($"주소 검색을 처리하지 못했습니다: {ex.Message}");
        }
    }

    public sealed record PartnerRow(
        string Code,
        string Name,
        string PartnerKind,
        string TransactionTypes,
        string Representative,
        string PhoneNumber,
        bool IsActive)
    {
        public string Status => IsActive ? "활성" : "비활성";
    }

    public sealed record PartnerKindOption(string Value, string DisplayName);

    public sealed record CountryOption(string Code, string DisplayName);

    public sealed record BankScopeOption(string Value, string DisplayName);

    public sealed record BankOption(string Code, string DisplayName);

    private sealed record PartnerEditorSnapshot(
        string Code,
        string CompanyName,
        string? PartnerKindValue,
        bool IsSales,
        bool IsSupply,
        bool IsReceivable,
        bool IsPayable,
        bool IsEtc,
        string BusinessRegistrationNumber,
        string IndividualName,
        string ResidentNumber,
        string RepresentativeName,
        string BusinessType,
        string BusinessCategory,
        string? CountryCode,
        string PostalCode,
        string Address,
        string DetailAddress,
        string PhoneNumber,
        string MobileNumber,
        string FaxNumber,
        string Email,
        string? BankScopeValue,
        string? DomesticBankCode,
        string? OverseasBankCode,
        string AccountNumber);

    public sealed partial class PartnerEditor : ObservableObject
    {
        [ObservableProperty]
        private string code = string.Empty;

        [ObservableProperty]
        private string companyName = string.Empty;

        [ObservableProperty]
        private PartnerKindOption? selectedPartnerKind;

        [ObservableProperty]
        private bool isSales;

        [ObservableProperty]
        private bool isSupply;

        [ObservableProperty]
        private bool isReceivable;

        [ObservableProperty]
        private bool isPayable;

        [ObservableProperty]
        private bool isEtc;

        [ObservableProperty]
        private string businessRegistrationNumber = string.Empty;

        [ObservableProperty]
        private string individualName = string.Empty;

        [ObservableProperty]
        private string residentNumber = string.Empty;

        [ObservableProperty]
        private string representativeName = string.Empty;

        [ObservableProperty]
        private string businessType = string.Empty;

        [ObservableProperty]
        private string businessCategory = string.Empty;

        [ObservableProperty]
        private CountryOption? selectedCountry;

        [ObservableProperty]
        private string postalCode = string.Empty;

        [ObservableProperty]
        private string address = string.Empty;

        [ObservableProperty]
        private string detailAddress = string.Empty;

        [ObservableProperty]
        private string phoneNumber = string.Empty;

        [ObservableProperty]
        private string mobileNumber = string.Empty;

        [ObservableProperty]
        private string faxNumber = string.Empty;

        [ObservableProperty]
        private string email = string.Empty;

        [ObservableProperty]
        private BankScopeOption? selectedBankScope;

        [ObservableProperty]
        private BankOption? selectedDomesticBank;

        [ObservableProperty]
        private BankOption? selectedOverseasBank;

        [ObservableProperty]
        private string accountNumber = string.Empty;
    }
}
