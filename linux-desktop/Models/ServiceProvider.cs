using System;

namespace TwoFactorAuth.Models;

public class ServiceProvider : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string? _iconPath;
    private string? _iconColor;
    private string? _description;
    private int _sortOrder;
    private DateTime _createdAt = DateTime.UtcNow;
    private bool _isBuiltIn;

    public Guid Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string? IconPath
    {
        get => _iconPath;
        set => SetField(ref _iconPath, value);
    }

    public string? IconColor
    {
        get => _iconColor;
        set => SetField(ref _iconColor, value);
    }

    public string? Description
    {
        get => _description;
        set => SetField(ref _description, value);
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

    public bool IsBuiltIn
    {
        get => _isBuiltIn;
        set => SetField(ref _isBuiltIn, value);
    }

    public override string ToString() => Name;
}
