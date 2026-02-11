using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using TwoFactorAuth.Models;
using TwoFactorAuth.Utils;

namespace TwoFactorAuth.ViewModels;

public sealed class ExportAccountViewModel : ViewModelBase
{
    private ExportMode _exportMode = ExportMode.Single;
    private ExportFormat _selectedFormat = ExportFormat.QrCode;
    private bool _isBusy;
    private string? _statusMessage;

    public ExportAccountViewModel(List<Account> accounts)
    {
        Accounts = new ObservableCollection<ExportAccountItem>(
            accounts.Select(a => new ExportAccountItem(a, this))
        );

        foreach (var item in Accounts)
        {
            item.IsSelected = true;
        }

        SetSingleModeCommand = new RelayCommand(() => ExportMode = ExportMode.Single);
        SetBatchModeCommand = new RelayCommand(() => ExportMode = ExportMode.Batch);
        NotifySelectionChangedCommand = new RelayCommand(NotifySelectionChanged);
    }

    public ObservableCollection<ExportAccountItem> Accounts { get; }

    public ICommand SetSingleModeCommand { get; }
    public ICommand SetBatchModeCommand { get; }
    public ICommand NotifySelectionChangedCommand { get; }

    public ExportMode ExportMode
    {
        get => _exportMode;
        set
        {
            if (SetField(ref _exportMode, value))
            {
                RaisePropertyChanged(nameof(IsSingleMode));
                RaisePropertyChanged(nameof(IsBatchMode));
                RaisePropertyChanged(nameof(CanSelectFormat));
                UpdateFormatAvailability();
            }
        }
    }

    public bool IsSingleMode => ExportMode == ExportMode.Single;
    public bool IsBatchMode => ExportMode == ExportMode.Batch;

    public ExportFormat SelectedFormat
    {
        get => _selectedFormat;
        set
        {
            if (SetField(ref _selectedFormat, value))
            {
                RaisePropertyChanged(nameof(IsQrCodeFormat));
                RaisePropertyChanged(nameof(IsUrlFormat));
            }
        }
    }

    public bool IsQrCodeFormat => SelectedFormat == ExportFormat.QrCode;
    public bool IsUrlFormat => SelectedFormat == ExportFormat.Url;

    public bool CanSelectFormat => IsSingleMode;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetField(ref _isBusy, value);
    }

    public string? StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public int SelectedCount => Accounts.Count(a => a.IsSelected);

    public void NotifySelectionChanged()
    {
        RaisePropertyChanged(nameof(SelectedCount));
    }

    private void UpdateFormatAvailability()
    {
        if (IsBatchMode && SelectedFormat == ExportFormat.Url)
        {
            SelectedFormat = ExportFormat.QrCode;
        }
    }

    public List<Account> GetSelectedAccounts()
    {
        return Accounts.Where(a => a.IsSelected).Select(a => a.Account).ToList();
    }
}

public sealed class ExportAccountItem : ViewModelBase
{
    private bool _isSelected;
    private readonly ExportAccountViewModel _parent;

    public ExportAccountItem(Account account, ExportAccountViewModel parent)
    {
        Account = account;
        _parent = parent;
    }

    public Account Account { get; }

    public string Name => Account.Name;
    public string Issuer => Account.Issuer ?? string.Empty;
    public string DisplayName => Name.Length > 30 ? Name.Substring(0, 30) + "..." : Name;
    public string DisplayIssuer => Issuer.Length > 30 ? Issuer.Substring(0, 30) + "..." : Issuer;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetField(ref _isSelected, value))
            {
                _parent.NotifySelectionChanged();
            }
        }
    }
}

public enum ExportMode
{
    Single,
    Batch
}

public enum ExportFormat
{
    QrCode,
    Url
}
