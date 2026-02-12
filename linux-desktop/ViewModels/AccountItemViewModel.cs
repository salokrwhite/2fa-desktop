using System;
using Avalonia.Media.Imaging;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;
using TwoFactorAuth.Utils;

namespace TwoFactorAuth.ViewModels;

public sealed class AccountItemViewModel : ViewModelBase
{
    private string _otp = string.Empty;
    private int _remainingSeconds;
    private bool _isSelected;
    private Bitmap? _issuerIconImage;

    public AccountItemViewModel(Account account, AccountListViewModel? parentViewModel = null)
    {
        Account = account;
        ParentViewModel = parentViewModel;
        ResolveIcon();
    }

    public Account Account { get; }
    public AccountListViewModel? ParentViewModel { get; set; }

    public Guid Id => Account.Id;
    public string Name => Account.Name;
    public string DisplayName => Name.Length > 20 ? Name.Substring(0, 20) + "..." : Name;
    public string Issuer => Account.Issuer;
    public string DisplayIssuer => Issuer?.Length > 20 ? Issuer.Substring(0, 20) + "..." : Issuer ?? string.Empty;
    public string? IssuerSvgContent { get; private set; }
    public Bitmap? IssuerIconImage
    {
        get
        {
            if (_issuerIconImage == null && !string.IsNullOrEmpty(IssuerSvgContent))
                _issuerIconImage = SvgImageHelper.FromSvgString(IssuerSvgContent, 64, 64);
            return _issuerIconImage;
        }
    }

    public bool HasIssuerIcon => !string.IsNullOrEmpty(IssuerSvgContent);

    public OtpType Type => Account.Type;
    public int Digits => Account.Digits;
    public int Period => Account.Period;
    public int Counter => Account.Counter;

    public bool IsFavorite
    {
        get => Account.IsFavorite;
        set
        {
            if (Account.IsFavorite != value)
            {
                Account.IsFavorite = value;
                RaisePropertyChanged();
            }
        }
    }

    public string Otp
    {
        get => _otp;
        set => SetField(ref _otp, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public int RemainingSeconds
    {
        get => _remainingSeconds;
        set
        {
            if (SetField(ref _remainingSeconds, value))
            {
                RaisePropertyChanged(nameof(RemainingLabel));
                RaisePropertyChanged(nameof(ProgressAngle));
            }
        }
    }

    public string RemainingLabel => Type == OtpType.Totp ? $"剩余 {RemainingSeconds}s" : "计数器模式";

    public double ProgressAngle => Period > 0 ? (double)RemainingSeconds / Period * 360.0 : 0;

    public void UpdateFrom(Account account)
    {
        Account.Name = account.Name;
        Account.Issuer = account.Issuer;
        Account.Secret = account.Secret;
        Account.Type = account.Type;
        Account.Digits = account.Digits;
        Account.Period = account.Period;
        Account.Counter = account.Counter;
        Account.Group = account.Group;
        Account.Icon = account.Icon;
        Account.IsFavorite = account.IsFavorite;

        ResolveIcon();

        RaisePropertyChanged(nameof(Name));
        RaisePropertyChanged(nameof(DisplayName));
        RaisePropertyChanged(nameof(Issuer));
        RaisePropertyChanged(nameof(DisplayIssuer));
        RaisePropertyChanged(nameof(IssuerSvgContent));
        RaisePropertyChanged(nameof(IssuerIconImage));
        RaisePropertyChanged(nameof(HasIssuerIcon));
    }

    public void NotifyIconColorChanged()
    {
        _issuerIconImage = null;
        RaisePropertyChanged(nameof(IssuerIconImage));
    }

    private void ResolveIcon()
    {
        _issuerIconImage = null;
        var match = BuiltInServiceProviders.MatchByName(Account.Issuer);
        if (match.HasValue)
        {
            IssuerSvgContent = match.Value.SvgContent;
        }
        else
        {
            IssuerSvgContent = null;
        }
    }
}
