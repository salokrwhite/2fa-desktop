using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TwoFactorAuth.Models;
using TwoFactorAuth.ViewModels;

using System.Collections.Generic;

namespace TwoFactorAuth.Views.Dialogs;

public partial class AddAccountDialog : Window
{
    private readonly Guid _editingId;

    public AddAccountDialog()
    {
        InitializeComponent();
    }

    public AddAccountDialog(List<Category> categories, List<ServiceProvider> serviceProviders)
    {
        InitializeComponent();
        var vm = new AddAccountViewModel();
        vm.InitCategories(categories);
        vm.InitServiceProviders(serviceProviders);
        DataContext = vm;
        _editingId = Guid.Empty;
    }

    public AddAccountDialog(Account account, List<Category> categories, List<ServiceProvider> serviceProviders)
    {
        InitializeComponent();
        var vm = new AddAccountViewModel();
        vm.InitCategories(categories);
        vm.InitServiceProviders(serviceProviders);
        vm.Load(account);
        DataContext = vm;
        _editingId = account.Id;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddAccountViewModel vm)
        {
            Close(null);
            return;
        }

        if (!int.TryParse(vm.Digits, out var digits)) digits = 6;
        if (!int.TryParse(vm.Period, out var period)) period = 30;

        var account = new Account
        {
            Id = _editingId == Guid.Empty ? Guid.NewGuid() : _editingId,
            Name = vm.Name.Trim(),
            Issuer = vm.Issuer.Trim(),
            Secret = vm.Secret.Trim().Replace(" ", string.Empty),
            Type = vm.Type,
            Digits = digits,
            Period = period,
            Group = vm.SelectedCategory?.Name,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        Close(account);
    }
}
