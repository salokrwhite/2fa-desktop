using System;
using System.ComponentModel.DataAnnotations;

namespace TwoFactorAuth.Models;

public class Account : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _issuer = string.Empty;
    private string _secret = string.Empty;
    private OtpType _type = OtpType.Totp;
    private int _digits = 6;
    private int _period = 30;
    private int _counter;
    private int _sortOrder;
    private DateTime _createdAt = DateTime.UtcNow;
    private DateTime _updatedAt = DateTime.UtcNow;
    private string? _group;
    private string? _icon;
    private bool _isFavorite;

    public Guid Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    [Required]
    [StringLength(100)]
    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    [StringLength(100)]
    public string Issuer
    {
        get => _issuer;
        set => SetField(ref _issuer, value);
    }

    [Required]
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

    public int Digits
    {
        get => _digits;
        set => SetField(ref _digits, value);
    }

    public int Period
    {
        get => _period;
        set => SetField(ref _period, value);
    }

    public int Counter
    {
        get => _counter;
        set => SetField(ref _counter, value);
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetField(ref _sortOrder, value);
    }

    public DateTime CreatedAt
    {
        get => _createdAt;
        set => SetField(ref _createdAt, value);
    }

    public DateTime UpdatedAt
    {
        get => _updatedAt;
        set => SetField(ref _updatedAt, value);
    }

    [StringLength(50)]
    public string? Group
    {
        get => _group;
        set => SetField(ref _group, value);
    }

    public string? Icon
    {
        get => _icon;
        set => SetField(ref _icon, value);
    }

    public bool IsFavorite
    {
        get => _isFavorite;
        set => SetField(ref _isFavorite, value);
    }
}

public enum OtpType
{
    Totp,
    Hotp
}
