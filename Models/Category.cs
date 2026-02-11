using System;

namespace TwoFactorAuth.Models;

public class Category : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
    private string _description = string.Empty;
    private int _sortOrder;

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

    public string Description
    {
        get => _description;
        set => SetField(ref _description, value);
    }

    public int SortOrder
    {
        get => _sortOrder;
        set => SetField(ref _sortOrder, value);
    }

    public override string ToString() => Name;
}
