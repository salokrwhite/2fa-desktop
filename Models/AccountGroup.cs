using System;

namespace TwoFactorAuth.Models;

public class AccountGroup : ObservableObject
{
    private Guid _id = Guid.NewGuid();
    private string _name = string.Empty;
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

    public int SortOrder
    {
        get => _sortOrder;
        set => SetField(ref _sortOrder, value);
    }
}
