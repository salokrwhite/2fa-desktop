using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.ViewModels;

public sealed class AddAccountViewModel : ViewModelBase
{
    public static readonly ServiceProvider CustomProviderSentinel = new()
    {
        Id = Guid.Empty,
        Name = "✎",
        IsBuiltIn = false
    };

    private string _name = string.Empty;
    private string _issuer = string.Empty;
    private string _secret = string.Empty;
    private OtpType _type = OtpType.Totp;
    private string _digits = "6";
    private string _period = "30";
    private Category? _selectedCategory;
    private ServiceProvider? _selectedServiceProvider;
    private bool _isCustomProvider;

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

    public ObservableCollection<Category> Categories { get; } = new();

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

    public void InitCategories(IEnumerable<Category> categories)
    {
        Categories.Clear();
        foreach (var c in categories) Categories.Add(c);
    }

    public void InitServiceProviders(IEnumerable<ServiceProvider> providers)
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

    public void Load(Account account)
    {
        Name = account.Name;
        Issuer = account.Issuer;
        Secret = account.Secret;
        Type = account.Type;
        Digits = account.Digits.ToString();
        Period = account.Period.ToString();
        
        if (!string.IsNullOrEmpty(account.Group))
        {
            SelectedCategory = Categories.FirstOrDefault(c => c.Name == account.Group);
        }

        if (!string.IsNullOrEmpty(account.Issuer))
        {
            var match = ServiceProviders.FirstOrDefault(p =>
                p != CustomProviderSentinel &&
                p.Name.Equals(account.Issuer, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                _selectedServiceProvider = match;
                _isCustomProvider = false;
            }
            else
            {
                _selectedServiceProvider = CustomProviderSentinel;
                _isCustomProvider = true;
            }
            RaisePropertyChanged(nameof(SelectedServiceProvider));
            RaisePropertyChanged(nameof(IsCustomProvider));
            RaisePropertyChanged(nameof(ShowIssuerInput));
        }
    }
}
