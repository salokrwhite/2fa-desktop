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

public sealed class QrImportViewModel : ViewModelBase
{
    private readonly HashSet<string> _existingSecretKeys;
    private readonly HashSet<string> _existingNameKeys;
    private readonly HashSet<string> _importSecretKeys = new(StringComparer.Ordinal);
    private readonly HashSet<string> _importNameKeys = new(StringComparer.Ordinal);
    private Category? _selectedCategory;
    private bool _isBusy;
    private bool _isUpdatingSelectAllState;
    private string _statusText = string.Empty;
    private bool? _selectAllState = false;

    public QrImportViewModel(IReadOnlyList<Category> categories, Category? selectedCategory, IReadOnlyList<Account> existingAccounts)
    {
        Categories = new ObservableCollection<Category>(categories);
        _selectedCategory = selectedCategory;

        _existingSecretKeys = new HashSet<string>(existingAccounts.Select(BuildSecretKey), StringComparer.Ordinal);
        _existingNameKeys = new HashSet<string>(existingAccounts.Select(BuildNameKey), StringComparer.Ordinal);
    }

    public ObservableCollection<Category> Categories { get; }

    public Category? SelectedCategory
    {
        get => _selectedCategory;
        set => SetField(ref _selectedCategory, value);
    }

    public ObservableCollection<QrImportItemViewModel> Items { get; } = new();

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                RaisePropertyChanged(nameof(CanImport));
                RaisePropertyChanged(nameof(CanSelectAll));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetField(ref _statusText, value);
    }

    public string SummaryText
    {
        get
        {
            var total = Items.Count;
            var selectable = Items.Count(x => x.CanSelect);
            var selected = Items.Count(x => x.CanSelect && x.IsSelected);
            return $"{selected}/{selectable} ({total})";
        }
    }

    public bool CanImport => !IsBusy && Items.Any(x => x.CanSelect && x.IsSelected);

    public bool CanSelectAll => !IsBusy && Items.Any(x => x.CanSelect);

    public bool? SelectAllState
    {
        get => _selectAllState;
        set
        {
            if (SetField(ref _selectAllState, value))
            {
                if (!_isUpdatingSelectAllState)
                {
                    ApplySelectAll(value);
                }
            }
        }
    }

    public void Clear()
    {
        foreach (var item in Items)
        {
            item.SelectionChanged -= OnItemSelectionChanged;
        }

        Items.Clear();
        _importSecretKeys.Clear();
        _importNameKeys.Clear();
        StatusText = string.Empty;
        SetSelectAllState(false);
        RaisePropertyChanged(nameof(SummaryText));
        RaisePropertyChanged(nameof(CanImport));
        RaisePropertyChanged(nameof(CanSelectAll));
    }

    public async Task AddImagesAsync(IEnumerable<QrImportImageSource> sources)
    {
        var list = sources.ToList();
        if (list.Count == 0) return;

        IsBusy = true;
        try
        {
            StatusText = string.Empty;
            foreach (var source in list)
            {
                var decoded = await DecodeImageAsync(source);
                if (decoded.Count == 0)
                {
                    decoded.Add(QrImportItemViewModel.CreateNoQrFound(source.DisplayName));
                }

                foreach (var item in decoded)
                {
                    item.SelectionChanged += OnItemSelectionChanged;
                    Items.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
        finally
        {
            IsBusy = false;
            UpdateSelectAllState();
            RaisePropertyChanged(nameof(SummaryText));
            RaisePropertyChanged(nameof(CanImport));
            RaisePropertyChanged(nameof(CanSelectAll));
        }
    }

    public List<Account> BuildAccountsForImport()
    {
        var group = SelectedCategory?.Name;
        var results = new List<Account>();
        foreach (var item in Items)
        {
            var account = item.BuildAccountForImport(group);
            if (account != null)
            {
                results.Add(account);
            }
        }
        return results;
    }

    private void OnItemSelectionChanged()
    {
        if (_isUpdatingSelectAllState)
        {
            return;
        }

        UpdateSelectAllState();
        RaisePropertyChanged(nameof(SummaryText));
        RaisePropertyChanged(nameof(CanImport));
    }

    private void ApplySelectAll(bool? value)
    {
        if (value is not true and not false) return;

        _isUpdatingSelectAllState = true;
        try
        {
            foreach (var item in Items)
            {
                if (item.CanSelect)
                {
                    item.IsSelected = value.Value;
                }
            }
        }
        finally
        {
            _isUpdatingSelectAllState = false;
        }

        UpdateSelectAllState();
        RaisePropertyChanged(nameof(SummaryText));
        RaisePropertyChanged(nameof(CanImport));
    }

    private void UpdateSelectAllState()
    {
        var selectable = Items.Where(x => x.CanSelect).ToList();
        if (selectable.Count == 0)
        {
            SetSelectAllState(false);
            return;
        }

        var selectedCount = selectable.Count(x => x.IsSelected);
        var state = selectedCount == 0 ? false : selectedCount == selectable.Count ? true : (bool?)null;
        SetSelectAllState(state);
    }

    private void SetSelectAllState(bool? state)
    {
        if (Equals(_selectAllState, state))
        {
            return;
        }

        _isUpdatingSelectAllState = true;
        try
        {
            SetField(ref _selectAllState, state, nameof(SelectAllState));
        }
        finally
        {
            _isUpdatingSelectAllState = false;
        }
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
        var isImportDuplicate = _importSecretKeys.Contains(secretKey) || _importNameKeys.Contains(nameKey);

        _importSecretKeys.Add(secretKey);
        _importNameKeys.Add(nameKey);

        if (isExistingDuplicate)
        {
            return QrImportItemViewModel.CreateDuplicate(sourceFile, rawText, account, UiText.Get("Lang.QrImport.Detail.AlreadyExists"));
        }

        if (isImportDuplicate)
        {
            return QrImportItemViewModel.CreateDuplicate(sourceFile, rawText, account, UiText.Get("Lang.QrImport.Detail.DuplicateInImport"));
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
}

public sealed class QrImportItemViewModel : ViewModelBase
{
    private bool _isSelected;

    private QrImportItemViewModel(
        string sourceFile,
        string rawText,
        string issuer,
        string name,
        string type,
        string digits,
        string period,
        string status,
        string statusDetails,
        bool canSelect,
        Account? parsedAccount,
        bool isSelected)
    {
        SourceFile = sourceFile;
        RawText = rawText;
        Issuer = issuer;
        Name = name;
        Type = type;
        Digits = digits;
        Period = period;
        Status = status;
        StatusDetails = statusDetails;
        CanSelect = canSelect;
        ParsedAccount = parsedAccount;
        _isSelected = isSelected;
    }

    public string SourceFile { get; }
    public string RawText { get; }
    public string Issuer { get; }
    public string Name { get; }
    public string Type { get; }
    public string Digits { get; }
    public string Period { get; }
    public string Status { get; }
    public string StatusDetails { get; }
    public bool CanSelect { get; }
    public Account? ParsedAccount { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!CanSelect) return;
            if (SetField(ref _isSelected, value))
            {
                SelectionChanged?.Invoke();
            }
        }
    }

    public event Action? SelectionChanged;

    public Account? BuildAccountForImport(string? group)
    {
        if (!CanSelect || !IsSelected || ParsedAccount == null) return null;

        var account = new Account
        {
            Name = ParsedAccount.Name,
            Issuer = ParsedAccount.Issuer,
            Secret = ParsedAccount.Secret,
            Digits = ParsedAccount.Digits,
            Period = ParsedAccount.Period,
            Counter = ParsedAccount.Counter,
            Type = ParsedAccount.Type,
            Group = group ?? ParsedAccount.Group,
            Icon = ParsedAccount.Icon,
            IsFavorite = ParsedAccount.IsFavorite,
            SortOrder = ParsedAccount.SortOrder
        };

        return account;
    }

    public static QrImportItemViewModel CreateNoQrFound(string sourceFile)
    {
        return new QrImportItemViewModel(
            sourceFile,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            UiText.Get("Lang.QrImport.Status.NoQrFound"),
            UiText.Get("Lang.QrImport.Status.NoQrFound"),
            false,
            null,
            false);
    }

    public static QrImportItemViewModel CreateInvalid(string sourceFile, string rawText, string message)
    {
        return new QrImportItemViewModel(
            sourceFile,
            rawText,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            UiText.Get("Lang.QrImport.Status.Invalid"),
            message,
            false,
            null,
            false);
    }

    public static QrImportItemViewModel CreateParsed(string sourceFile, string rawText, Account account)
    {
        return new QrImportItemViewModel(
            sourceFile,
            rawText,
            account.Issuer ?? string.Empty,
            account.Name,
            account.Type.ToString(),
            account.Digits.ToString(),
            account.Period.ToString(),
            UiText.Get("Lang.QrImport.Status.Ready"),
            string.Empty,
            true,
            account,
            true);
    }

    public static QrImportItemViewModel CreateDuplicate(string sourceFile, string rawText, Account account, string message)
    {
        return new QrImportItemViewModel(
            sourceFile,
            rawText,
            account.Issuer ?? string.Empty,
            account.Name,
            account.Type.ToString(),
            account.Digits.ToString(),
            account.Period.ToString(),
            UiText.Get("Lang.QrImport.Status.Duplicate"),
            message,
            true,
            account,
            false);
    }
}

public sealed record QrImportImageSource(string DisplayName, Func<Task<System.IO.Stream>> OpenRead);

internal static class UiText
{
    internal static string Get(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
        {
            return s;
        }
        return key;
    }
}
