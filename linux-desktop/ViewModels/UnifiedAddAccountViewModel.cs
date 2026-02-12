using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using TwoFactorAuth.Models;
using TwoFactorAuth.Utils;

namespace TwoFactorAuth.ViewModels;

public sealed class UnifiedAddAccountViewModel : ViewModelBase
{
    public static readonly ServiceProvider CustomProviderSentinel = new()
    {
        Id = Guid.Empty,
        Name = "✎",
        IsBuiltIn = false
    };

    private int _selectedTabIndex;
    private string _name = string.Empty;
    private string _issuer = string.Empty;
    private string _secret = string.Empty;
    private OtpType _type = OtpType.Totp;
    private string _digits = "6";
    private string _period = "30";
    private Category? _selectedCategory;
    private ServiceProvider? _selectedServiceProvider;
    private bool _isCustomProvider;
    private string _otpAuthUrl = string.Empty;
    private string _urlParseStatus = string.Empty;
    private bool _isUrlValid;
    private bool _isBusy;
    private string _qrStatusText = string.Empty;

    private readonly HashSet<string> _existingSecretKeys;
    private readonly HashSet<string> _existingNameKeys;

    public UnifiedAddAccountViewModel(IReadOnlyList<Category> categories, IReadOnlyList<ServiceProvider> serviceProviders, IReadOnlyList<Account> existingAccounts)
    {
        Categories = new ObservableCollection<Category>(categories);
        InitServiceProviders(serviceProviders);

        _existingSecretKeys = new HashSet<string>(existingAccounts.Select(BuildSecretKey), StringComparer.Ordinal);
        _existingNameKeys = new HashSet<string>(existingAccounts.Select(BuildNameKey), StringComparer.Ordinal);
    }

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set => SetField(ref _selectedTabIndex, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Issuer
    {
        get => _issuer;
        set => SetField(ref _issuer, value);
    }

    public string Secret
    {
        get => _secret;
        set => SetField(ref _secret, value);
    }

    public OtpType Type
    {
        get => _type;
        set => SetField(ref _type, value);
    }

    public string Digits
    {
        get => _digits;
        set => SetField(ref _digits, value);
    }

    public string Period
    {
        get => _period;
        set => SetField(ref _period, value);
    }

    public ObservableCollection<Category> Categories { get; }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set => SetField(ref _selectedCategory, value);
    }

    public ObservableCollection<ServiceProvider> ServiceProviders { get; } = new();

    public bool IsCustomProvider
    {
        get => _isCustomProvider;
        private set => SetField(ref _isCustomProvider, value);
    }

    public bool ShowIssuerInput => IsCustomProvider;

    public ServiceProvider? SelectedServiceProvider
    {
        get => _selectedServiceProvider;
        set
        {
            if (SetField(ref _selectedServiceProvider, value))
            {
                if (value != null && value != CustomProviderSentinel)
                {
                    Issuer = value.Name;
                    IsCustomProvider = false;
                }
                else if (value == CustomProviderSentinel)
                {
                    Issuer = string.Empty;
                    IsCustomProvider = true;
                }
                else
                {
                    IsCustomProvider = false;
                }
                RaisePropertyChanged(nameof(ShowIssuerInput));
            }
        }
    }

    public IReadOnlyList<OtpType> OtpTypes { get; } = new[] { OtpType.Totp, OtpType.Hotp };

    public string OtpAuthUrl
    {
        get => _otpAuthUrl;
        set
        {
            if (SetField(ref _otpAuthUrl, value))
            {
                ValidateAndParseUrl();
            }
        }
    }

    public string UrlParseStatus
    {
        get => _urlParseStatus;
        private set => SetField(ref _urlParseStatus, value);
    }

    public bool IsUrlValid
    {
        get => _isUrlValid;
        private set
        {
            if (SetField(ref _isUrlValid, value))
            {
                RaisePropertyChanged(nameof(UrlParseStatusColor));
            }
        }
    }

    public string UrlParseStatusColor => IsUrlValid ? "#4CAF50" : "#F44336";

    public ObservableCollection<QrImportItemViewModel> QrItems { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetField(ref _isBusy, value);
    }

    public string QrStatusText
    {
        get => _qrStatusText;
        private set => SetField(ref _qrStatusText, value);
    }

    public string QrSummaryText
    {
        get
        {
            var total = QrItems.Count;
            var selectable = QrItems.Count(x => x.CanSelect);
            var selected = QrItems.Count(x => x.CanSelect && x.IsSelected);
            return $"{selected}/{selectable} ({total})";
        }
    }

    private void InitServiceProviders(IEnumerable<ServiceProvider> providers)
    {
        ServiceProviders.Clear();
        var sortedProviders = providers.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var p in sortedProviders) ServiceProviders.Add(p);

        if (Avalonia.Application.Current?.TryGetResource("Lang.ServiceProvider.Custom", null, out var res) == true && res is string s)
            CustomProviderSentinel.Name = s;
        else
            CustomProviderSentinel.Name = "✎ Custom Provider";

        ServiceProviders.Add(CustomProviderSentinel);
    }

    private void ValidateAndParseUrl()
    {
        if (string.IsNullOrWhiteSpace(_otpAuthUrl))
        {
            UrlParseStatus = string.Empty;
            IsUrlValid = false;
            return;
        }

        var trimmed = _otpAuthUrl.Trim();

        if (trimmed.StartsWith("otpauth-migration://", StringComparison.OrdinalIgnoreCase))
        {
            if (GoogleAuthMigrationParser.TryParse(trimmed, out var accounts, out var error))
            {
                if (accounts.Count > 0)
                {
                    UrlParseStatus = $"解析成功：找到 {accounts.Count} 个账号";
                    IsUrlValid = true;
                    LoadAccountData(accounts[0]);
                }
                else
                {
                    UrlParseStatus = "未找到账号数据";
                    IsUrlValid = false;
                }
            }
            else
            {
                UrlParseStatus = $"解析失败：{error}";
                IsUrlValid = false;
            }
            return;
        }

        if (TryParseOtpAuth(trimmed, out var account, out var parseError))
        {
            UrlParseStatus = "解析成功";
            IsUrlValid = true;
            LoadAccountData(account);
        }
        else
        {
            UrlParseStatus = parseError;
            IsUrlValid = false;
        }
    }

    private void LoadAccountData(Account account)
    {
        Name = account.Name;
        Issuer = account.Issuer ?? string.Empty;
        Secret = account.Secret;
        Type = account.Type;
        Digits = account.Digits.ToString();
        Period = account.Period.ToString();

        var match = ServiceProviders.FirstOrDefault(p =>
            p != CustomProviderSentinel &&
            p.Name.Equals(account.Issuer, StringComparison.OrdinalIgnoreCase));

        if (match != null)
        {
            SelectedServiceProvider = match;
        }
        else if (!string.IsNullOrEmpty(account.Issuer))
        {
            SelectedServiceProvider = CustomProviderSentinel;
        }
    }

    public async Task AddQrImagesAsync(IEnumerable<QrImportImageSource> sources)
    {
        var list = sources.ToList();
        if (list.Count == 0) return;

        IsBusy = true;
        try
        {
            QrStatusText = string.Empty;
            foreach (var source in list)
            {
                var decoded = await DecodeImageAsync(source);
                if (decoded.Count == 0)
                {
                    decoded.Add(QrImportItemViewModel.CreateNoQrFound(source.DisplayName));
                }

                foreach (var item in decoded)
                {
                    QrItems.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            QrStatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            RaisePropertyChanged(nameof(QrSummaryText));
        }
    }

    public void ClearQrImages()
    {
        QrItems.Clear();
        QrStatusText = string.Empty;
        RaisePropertyChanged(nameof(QrSummaryText));
    }

    private async Task<List<QrImportItemViewModel>> DecodeImageAsync(QrImportImageSource source)
    {
        await using var stream = await source.OpenRead();
        using var bitmap = new Bitmap(stream);

        var texts = QrCodeDecoder.DecodeQrTexts(bitmap);
        if (texts.Count == 0) return new List<QrImportItemViewModel>();

        var results = new List<QrImportItemViewModel>(texts.Count);
        foreach (var text in texts)
        {
            var trimmed = text.Trim();

            if (trimmed.StartsWith("otpauth-migration://", StringComparison.OrdinalIgnoreCase))
            {
                if (GoogleAuthMigrationParser.TryParse(trimmed, out var accounts, out var migrationError))
                {
                    foreach (var account in accounts)
                    {
                        results.Add(CreateParsedOrDuplicate(source.DisplayName, trimmed, account));
                    }
                }
                else
                {
                    results.Add(QrImportItemViewModel.CreateInvalid(source.DisplayName, trimmed, migrationError));
                }
            }
            else if (TryParseOtpAuth(trimmed, out var account, out var error))
            {
                results.Add(CreateParsedOrDuplicate(source.DisplayName, trimmed, account));
            }
            else
            {
                results.Add(QrImportItemViewModel.CreateInvalid(source.DisplayName, trimmed, error));
            }
        }

        return results;
    }

    private QrImportItemViewModel CreateParsedOrDuplicate(string sourceFile, string rawText, Account account)
    {
        var secretKey = BuildSecretKey(account);
        var nameKey = BuildNameKey(account);

        var isExistingDuplicate = _existingSecretKeys.Contains(secretKey) || _existingNameKeys.Contains(nameKey);

        if (isExistingDuplicate)
        {
            return QrImportItemViewModel.CreateDuplicate(sourceFile, rawText, account, UiText.Get("Lang.QrImport.Detail.AlreadyExists"));
        }

        return QrImportItemViewModel.CreateParsed(sourceFile, rawText, account);
    }

    private static string BuildSecretKey(Account account)
    {
        var secret = (account.Secret ?? string.Empty).Trim().Replace(" ", string.Empty).ToUpperInvariant();
        return $"{(int)account.Type}|{secret}";
    }

    private static string BuildNameKey(Account account)
    {
        var issuer = (account.Issuer ?? string.Empty).Trim().ToUpperInvariant();
        var name = (account.Name ?? string.Empty).Trim().ToUpperInvariant();
        return $"{(int)account.Type}|{issuer}|{name}";
    }

    private static bool TryParseOtpAuth(string text, out Account account, out string error)
    {
        account = new Account();
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = UiText.Get("Lang.QrImport.Detail.EmptyQrText");
            return false;
        }

        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            error = UiText.Get("Lang.QrImport.Detail.InvalidUri");
            return false;
        }

        if (!uri.Scheme.Equals("otpauth", StringComparison.OrdinalIgnoreCase))
        {
            error = UiText.Get("Lang.QrImport.Detail.NotOtpAuth");
            return false;
        }

        if (!uri.Host.Equals("totp", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.Equals("hotp", StringComparison.OrdinalIgnoreCase))
        {
            error = UiText.Get("Lang.QrImport.Detail.UnsupportedOtpType");
            return false;
        }

        try
        {
            account = OtpUriParser.Parse(text);
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        account.Secret = (account.Secret ?? string.Empty).Trim().Replace(" ", string.Empty);
        if (string.IsNullOrWhiteSpace(account.Secret))
        {
            error = UiText.Get("Lang.QrImport.Detail.MissingSecret");
            return false;
        }

        if (account.Digits <= 0 || account.Digits > 10)
        {
            error = UiText.Get("Lang.QrImport.Detail.InvalidDigits");
            return false;
        }

        if (account.Type == OtpType.Totp && (account.Period <= 0 || account.Period > 600))
        {
            error = UiText.Get("Lang.QrImport.Detail.InvalidPeriod");
            return false;
        }

        if (string.IsNullOrWhiteSpace(account.Name))
        {
            error = UiText.Get("Lang.QrImport.Detail.MissingAccountName");
            return false;
        }

        return true;
    }

    public Account? BuildAccountFromManualInput()
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Secret))
        {
            return null;
        }

        if (!int.TryParse(Digits, out var digits)) digits = 6;
        if (!int.TryParse(Period, out var period)) period = 30;

        return new Account
        {
            Id = Guid.NewGuid(),
            Name = Name.Trim(),
            Issuer = Issuer.Trim(),
            Secret = Secret.Trim().Replace(" ", string.Empty),
            Type = Type,
            Digits = digits,
            Period = period,
            Group = SelectedCategory?.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public List<Account> BuildAccountsFromQrImport()
    {
        var group = SelectedCategory?.Name;
        var results = new List<Account>();
        foreach (var item in QrItems)
        {
            var account = item.BuildAccountForImport(group);
            if (account != null)
            {
                results.Add(account);
            }
        }
        return results;
    }
}
