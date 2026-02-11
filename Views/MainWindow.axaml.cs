using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;
using TwoFactorAuth.ViewModels;
using TwoFactorAuth.Views.Dialogs;

namespace TwoFactorAuth.Views;

public partial class MainWindow : Window
{
    private IClipboardClearService? _clipboardClearService;
    private IMessageService? _messageService;
    private CategoryDragDropService? _dragDropService;
    private DataGrid? _logDataGrid;
    private DataGridColumn? _detailsColumn;

    public MainWindow()
    {
        InitializeComponent();
        _dragDropService = new CategoryDragDropService(this);
        _dragDropService.DropAsync = HandleCategoryDropAsync;
        this.DataContextChanged += OnDataContextChanged;
    }

    private void TryAttachLogDataGrid(DataGrid dataGrid)
    {
        if (_logDataGrid == dataGrid) return;
        if (_logDataGrid != null)
        {
            _logDataGrid.SizeChanged -= OnLogDataGridSizeChanged;
            _logDataGrid.LoadingRow -= OnLogDataGridLoadingRow;
        }
        
        _logDataGrid = dataGrid;
        if (_logDataGrid.Columns.Count > 0)
            _detailsColumn = _logDataGrid.Columns[_logDataGrid.Columns.Count - 1];
        
        _logDataGrid.SizeChanged += OnLogDataGridSizeChanged;
        _logDataGrid.LoadingRow += OnLogDataGridLoadingRow;
        
        Avalonia.Threading.Dispatcher.UIThread.Post(AdjustLastColumnWidth, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void AdjustLastColumnWidth()
    {
        if (_logDataGrid == null || _detailsColumn == null) return;
        
        double availableWidth = _logDataGrid.Bounds.Width;
        if (availableWidth <= 0) return;
        double otherColumnsWidth = 0;
        for (int i = 0; i < _logDataGrid.Columns.Count - 1; i++)
        {
            otherColumnsWidth += _logDataGrid.Columns[i].ActualWidth;
        }
        
        double lastColumnActual = _detailsColumn.ActualWidth;
        double totalUsed = otherColumnsWidth + lastColumnActual;
        if (totalUsed < availableWidth)
        {
            double needed = availableWidth - otherColumnsWidth - 2;
            if (needed > _detailsColumn.MinWidth)
            {
                _detailsColumn.Width = new DataGridLength(needed, DataGridLengthUnitType.Pixel);
            }
        }
        else
        {
            _detailsColumn.Width = DataGridLength.SizeToCells;
        }
    }

    private void OnLogDataGridSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        AdjustLastColumnWidth();
    }

    private void OnLogDataGridLoadingRow(object? sender, DataGridRowEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(AdjustLastColumnWidth, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void OnLogDataGridAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is DataGrid dg)
        {
            TryAttachLogDataGrid(dg);
        }
    }

    public void SetMessageService(IMessageService messageService)
    {
        _messageService = messageService;
        messageService.MessageRequested += OnMessageRequested;
    }

    private System.Threading.CancellationTokenSource? _messageCts;

    private async void OnMessageRequested(object? sender, MessageEventArgs e)
    {
        _messageCts?.Cancel();
        _messageCts = new System.Threading.CancellationTokenSource();
        var cts = _messageCts;

        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            var messageBorder = this.FindControl<Border>("MessageBorder");
            var messageIcon = this.FindControl<PathIcon>("MessageIcon");
            var messageTextBlock = this.FindControl<TextBlock>("MessageTextBlock");

            if (messageBorder == null || messageIcon == null || messageTextBlock == null)
                return;

            switch (e.Type)
            {
                case MessageType.Warning:
                    messageBorder.Background = Avalonia.Media.Brush.Parse("#FFF8E1");
                    messageIcon.Foreground = Avalonia.Media.Brush.Parse("#F57C00");
                    messageIcon.Data = Avalonia.Media.Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-2h2v2zm0-4h-2V7h2v6z");
                    break;
                case MessageType.Success:
                    messageBorder.Background = Avalonia.Media.Brush.Parse("#F1F8E9");
                    messageIcon.Foreground = Avalonia.Media.Brush.Parse("#4CAF50");
                    messageIcon.Data = Avalonia.Media.Geometry.Parse("M9 16.2L4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4L9 16.2z");
                    break;
                case MessageType.Error:
                    messageBorder.Background = Avalonia.Media.Brush.Parse("#FFEBEE");
                    messageIcon.Foreground = Avalonia.Media.Brush.Parse("#F44336");
                    messageIcon.Data = Avalonia.Media.Geometry.Parse("M12 2C6.47 2 2 6.47 2 12s4.47 10 10 10 10-4.47 10-10S17.53 2 12 2zm5 13.59L15.59 17 12 13.41 8.41 17 7 15.59 10.59 12 7 8.41 8.41 7 12 10.59 15.59 7 17 8.41 13.41 12 17 15.59z");
                    break;
                case MessageType.Info:
                    messageBorder.Background = Avalonia.Media.Brush.Parse("#E3F2FD");
                    messageIcon.Foreground = Avalonia.Media.Brush.Parse("#2196F3");
                    messageIcon.Data = Avalonia.Media.Geometry.Parse("M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z");
                    break;
            }

            messageTextBlock.Text = e.Message;
            messageBorder.Opacity = 0;
            messageBorder.Margin = new Avalonia.Thickness(0, 14, 0, 0);
            messageBorder.IsVisible = true;
            await System.Threading.Tasks.Task.Delay(16);
            if (cts.Token.IsCancellationRequested) return;

            messageBorder.Opacity = 1;
            messageBorder.Margin = new Avalonia.Thickness(0, 24, 0, 0);
            await System.Threading.Tasks.Task.Delay(3000);
            if (cts.Token.IsCancellationRequested) return;
            messageBorder.Opacity = 0;
            messageBorder.Margin = new Avalonia.Thickness(0, 14, 0, 0);
            await System.Threading.Tasks.Task.Delay(300);
            if (cts.Token.IsCancellationRequested) return;

            messageBorder.IsVisible = false;
        });
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.BackupVM.SetParentWindow(this);
            vm.BackupVM.SetMainViewModel(vm);
            _ = vm.BackupVM.LoadDataAsync();
            vm.QrImportRequested += OnQrImportRequested;
        }
    }

    private async void OnQrImportRequested(object? sender, System.EventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var categories = vm.AccountListVM.Categories.ToList();
        var serviceProviders = await vm.ServiceProviderListVM.GetAllProvidersAsync();
        var existingAccounts = vm.AccountListVM.Accounts.Select(x => x.Account).ToList();

        var dialog = new UnifiedAddAccountDialog(categories, serviceProviders, existingAccounts);
        if (dialog.DataContext is UnifiedAddAccountViewModel dialogVm)
        {
            dialogVm.SelectedTabIndex = 2; 
        }
        var accounts = await dialog.ShowDialog<List<Account>?>(this);
        if (accounts is { Count: > 0 })
        {
            await vm.AccountListVM.AddAccountsAsync(accounts);
        }
    }

    public void SetClipboardClearService(IClipboardClearService clipboardClearService)
    {
        _clipboardClearService = clipboardClearService;
    }

    private async void OnQrImportClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var categories = vm.AccountListVM.Categories.ToList();
        var serviceProviders = await vm.ServiceProviderListVM.GetAllProvidersAsync();
        var existingAccounts = vm.AccountListVM.Accounts.Select(x => x.Account).ToList();

        var dialog = new UnifiedAddAccountDialog(categories, serviceProviders, existingAccounts);
        if (dialog.DataContext is UnifiedAddAccountViewModel dialogVm)
        {
            dialogVm.SelectedTabIndex = 2; 
        }
        var accounts = await dialog.ShowDialog<List<Account>?>(this);
        if (accounts is { Count: > 0 })
        {
            await vm.AccountListVM.AddAccountsAsync(accounts);
        }
    }

    private async void OnAddClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        var categories = vm.AccountListVM.Categories.ToList();
        var serviceProviders = await vm.ServiceProviderListVM.GetAllProvidersAsync();
        var existingAccounts = vm.AccountListVM.Accounts.Select(a => a.Account).ToList();

        var dialog = new UnifiedAddAccountDialog(categories, serviceProviders, existingAccounts);
        var accounts = await dialog.ShowDialog<List<Account>?>(this);
        if (accounts != null && accounts.Count > 0)
        {
            if (accounts.Count == 1)
            {
                await vm.AddAccountAsync(accounts[0]);
            }
            else
            {
                await vm.AccountListVM.AddAccountsAsync(accounts);
            }
        }
    }

    private async void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || DataContext is not MainViewModel vm)
        {
            return;
        }

        if (control.DataContext is not AccountItemViewModel item)
        {
            return;
        }

        var categories = vm.AccountListVM.Categories.ToList();
        var serviceProviders = await vm.ServiceProviderListVM.GetAllProvidersAsync();
        var dialog = new AddAccountDialog(item.Account, categories, serviceProviders);
        var updated = await dialog.ShowDialog<Account?>(this);
        if (updated != null)
        {
            await vm.UpdateAccountAsync(updated);
        }
    }

    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || DataContext is not MainViewModel vm)
        {
            return;
        }

        if (control.DataContext is not AccountItemViewModel item)
        {
            return;
        }

        var title = "Delete Account";
        var message = "Are you sure you want to delete this account? This action cannot be undone.";
        
        if (Application.Current?.TryGetResource("Lang.DeleteConfirmTitle", null, out var t) == true && t is string tStr) title = tStr;
        if (Application.Current?.TryGetResource("Lang.DeleteConfirmMessage", null, out var m) == true && m is string mStr) message = mStr;

        var dialog = new ConfirmDialog(title, message);
        var result = await dialog.ShowDialog<bool>(this);
        
        if (result)
        {
            var accountName = item.Name;
            await vm.DeleteAccountAsync(item);

            var successMsg = $"Successfully deleted {accountName}";
            if (Application.Current?.TryGetResource("Lang.DeleteSuccessFormat", null, out var s) == true && s is string sStr)
                successMsg = string.Format(sStr, accountName);
            _messageService?.ShowSuccess(successMsg);
        }
    }

    private async void OnCategoryAccountDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not CategoryAccountItemViewModel item) return;

        var title = "Delete Account";
        var message = "Are you sure?";
        
        if (Application.Current?.TryGetResource("Lang.DeleteConfirmTitle", null, out var t) == true && t is string tStr) title = tStr;
        if (Application.Current?.TryGetResource("Lang.DeleteConfirmMessage", null, out var m) == true && m is string mStr) message = mStr;

        var dialog = new ConfirmDialog(title, message);
        var result = await dialog.ShowDialog<bool>(this);
        
        if (result)
        {
            item.Delete();
        }
    }

    private async void OnPinClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || DataContext is not MainViewModel vm) return;
        if (control.DataContext is not AccountItemViewModel item) return;

        await vm.AccountListVM.TogglePinAsync(item);
    }

    private void OnToggleSelectionModeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        vm.AccountListVM.ToggleSelectionMode();
    }
    private void OnSelectAllClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (vm.AccountListVM.IsAllSelected)
        {
            vm.AccountListVM.DeselectAll();
        }
        else
        {
            vm.AccountListVM.SelectAll();
        }
    }

    private async void OnDeleteSelectedClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var selected = vm.AccountListVM.Accounts.Where(x => x.IsSelected).ToList();
        if (selected.Count == 0) return;

        var title = "Delete Selected Accounts";
        var message = $"Are you sure you want to delete {selected.Count} selected accounts? This action cannot be undone.";
        
        if (Application.Current?.TryGetResource("Lang.DeleteSelectedConfirmTitle", null, out var t) == true && t is string tStr) title = tStr;
        if (Application.Current?.TryGetResource("Lang.DeleteSelectedConfirmMessage", null, out var m) == true && m is string mStr)
            message = string.Format(mStr, selected.Count);

        var dialog = new ConfirmDialog(title, message);
        var result = await dialog.ShowDialog<bool>(this);
        
        if (result)
        {
            var names = string.Join(", ", selected.Select(x => x.Name));
            await vm.AccountListVM.DeleteSelectedAsync();

            var successMsg = $"Successfully deleted {names}";
            if (Application.Current?.TryGetResource("Lang.DeleteSuccessFormat", null, out var s) == true && s is string sStr)
                successMsg = string.Format(sStr, names);
            _messageService?.ShowSuccess(successMsg);
        }
    }

    private async void OnMoveToCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || DataContext is not MainViewModel vm) return;
        if (control.DataContext is not AccountItemViewModel item) return;

        var categories = vm.AccountListVM.Categories.ToList();
        var dialog = new MoveToCategoryDialog(categories);
        var category = await dialog.ShowDialog<Category?>(this);
        
        if (category != null)
        {
            item.Account.Group = category.Name;
            await vm.UpdateAccountAsync(item.Account);
            await vm.CategoryListVM.LoadAsync();
        }
    }

    private async void OnBatchMoveToCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var categories = vm.AccountListVM.Categories.ToList();
        var dialog = new MoveToCategoryDialog(categories);
        var category = await dialog.ShowDialog<Category?>(this);
        
        if (category != null)
        {
            await vm.AccountListVM.MoveSelectedToCategoryAsync(category);
            await vm.CategoryListVM.LoadAsync();
        }
    }

    private void OnDetailsClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not AccountItemViewModel item) return;

        var dialog = new AccountDetailDialog(item.Account);
        _ = dialog.ShowDialog(this);
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        if (button.DataContext is not AccountItemViewModel item) return;
        await CopyOtpToClipboard(item);
    }

    private async void OnCardPointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(null).Properties.IsLeftButtonPressed) return;
        if (e.Source is Avalonia.Controls.Primitives.ToggleButton or Button or CheckBox) return;
        var source = e.Source as Control;
        while (source != null)
        {
            if (source is Button or CheckBox) return;
            source = source.Parent as Control;
        }

        if (sender is not Border border) return;
        if (border.DataContext is not AccountItemViewModel item) return;
        await CopyOtpToClipboard(item);
    }

    private async Task CopyOtpToClipboard(AccountItemViewModel item)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(item.Otp);
            _clipboardClearService?.ScheduleClear(item.Otp);

            var successMsg = "Copied";
            if (Application.Current?.TryGetResource("Lang.CopySuccess", null, out var s) == true && s is string sStr)
                successMsg = sStr;
            _messageService?.ShowSuccess(successMsg);
        }
    }

    private async void OnLanguageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var dialog = new LanguageSelectionDialog(vm.CurrentLanguage);
        var result = await dialog.ShowDialog<string?>(this);

        if (!string.IsNullOrEmpty(result))
        {
            vm.ChangeLanguage(result);
        }
    }

    private async void OnCategorySettingsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        CategoryItemViewModel? categoryVm = null;
        if (sender is Button button)
        {
            categoryVm = button.CommandParameter as CategoryItemViewModel;
        }
        else if (sender is MenuItem menuItem)
        {
            categoryVm = menuItem.DataContext as CategoryItemViewModel;
        }
        
        if (categoryVm == null) return;

        var dialog = new CategorySettingsDialog(categoryVm.Name, categoryVm.Description);
        var result = await dialog.ShowDialog<CategorySettingsResult?>(this);
        
        if (result is not null)
        {
            await vm.CategoryListVM.UpdateCategoryAsync(categoryVm, result.Name, result.Description);
        }
    }

    private async void OnCategoryDetailClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || DataContext is not MainViewModel vm) return;
        if (button.CommandParameter is not CategoryItemViewModel categoryVm) return;

        var dialog = new CategoryDetailDialog(categoryVm);
        await dialog.ShowDialog(this);
    }

    private void OnCategoryCheckboxChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || DataContext is not MainViewModel vm) return;
        if (checkBox.DataContext is not CategoryItemViewModel categoryVm) return;

        if (checkBox.IsChecked == true)
        {
            vm.CategoryListVM.SelectedCategories.Add(categoryVm);
        }
        else
        {
            vm.CategoryListVM.SelectedCategories.Remove(categoryVm);
        }
    }

    private async void OnMergeCategoriesClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        
        var selectedCategories = vm.CategoryListVM.SelectedCategories.ToList();
        if (selectedCategories.Count < 2)
            return;

        var dialog = new MergeCategoriesDialog(selectedCategories);
        var targetCategory = await dialog.ShowDialog<CategoryItemViewModel?>(this);
        
        if (targetCategory != null)
        {
            await PerformMergeAsync(vm, selectedCategories, targetCategory);
        }
    }

    private async System.Threading.Tasks.Task PerformMergeAsync(
        MainViewModel vm, 
        List<CategoryItemViewModel> selectedCategories, 
        CategoryItemViewModel targetCategory)
    {
        var categoriesToDelete = selectedCategories.Where(c => c != targetCategory).ToList();
        var allAccounts = await vm.CategoryListVM._accountService.GetAllAccountsAsync();
        foreach (var category in categoriesToDelete)
        {
            var accountsToMove = allAccounts.Where(a => a.Group == category.Name).ToList();
            foreach (var account in accountsToMove)
            {
                account.Group = targetCategory.Name;
                await vm.CategoryListVM._accountService.UpdateAccountAsync(account);
            }
            
            await vm.CategoryListVM.LogMergeOperationAsync(category.Name, targetCategory.Name);
        }

        foreach (var category in categoriesToDelete)
        {
            await vm.CategoryListVM.DeleteCategoryAsync(category);
        }

        vm.CategoryListVM.SelectedCategories.Clear();
        vm.CategoryListVM.IsMultiSelectMode = false;

        await vm.CategoryListVM.LoadAsync();
        await vm.AccountListVM.LoadAsync();
    }

    private async void OnViewCategoryPropertiesClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem) return;
        if (menuItem.DataContext is not CategoryItemViewModel categoryVm) return;

        var dialog = new CategoryDetailDialog(categoryVm);
        await dialog.ShowDialog(this);
    }

    [System.Obsolete("Use OnCategorySettingsClick instead")]
    private async void OnRenameCategoryClick(object? sender, RoutedEventArgs e)
    {
        OnCategorySettingsClick(sender, e);
    }

    private void OnCategoryDragStart(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.Tag is not CategoryItemViewModel categoryVm) return;
        _dragDropService?.OnDragStart(sender, e, categoryVm);
    }

    private void OnCategoryDragMove(object? sender, PointerEventArgs e)
    {
        _dragDropService?.OnDragMove(sender, e);
    }

    private async System.Threading.Tasks.Task HandleCategoryDropAsync(
        CategoryItemViewModel source,
        CategoryItemViewModel target)
    {
        if (DataContext is not MainViewModel vm) return;
        await vm.CategoryListVM.HandleDropAsync(source, target);
    }

    private async void OnServiceProviderIconSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        if (sender is not ComboBox comboBox) return;

        var selected = comboBox.SelectedItem as IconOption;
        if (selected?.IsUploadOption != true) return;

        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = GetResource("Lang.ServiceProvider.SelectSvg"),
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType(GetResource("Lang.ServiceProvider.SvgFiles"))
                {
                    Patterns = new[] { "*.svg" }
                }
            }
        });

        if (files.Count > 0)
        {
            var filePath = files[0].Path.LocalPath;
            System.Diagnostics.Debug.WriteLine($"[SVG Upload] File path: {filePath}");
            System.Diagnostics.Debug.WriteLine($"[SVG Upload] IsValidSvg: {Utils.SvgParser.IsValidSvg(filePath)}");

            if (Utils.SvgParser.IsValidSvg(filePath))
            {
                var svgContent = System.IO.File.ReadAllText(filePath);
                if (!string.IsNullOrEmpty(svgContent))
                {
                    System.Diagnostics.Debug.WriteLine($"[SVG Upload] SVG content length: {svgContent.Length}");

                    var uploadedIcon = new IconOption(
                        GetResource("Lang.ServiceProvider.UploadedIcon"),
                        svgContent);
                    var existing = vm.ServiceProviderListVM.AvailableIcons
                        .FirstOrDefault(i => !i.IsUploadOption && i.SvgContent != null && i.DisplayName == GetResource("Lang.ServiceProvider.UploadedIcon"));
                    if (existing != null)
                        vm.ServiceProviderListVM.AvailableIcons.Remove(existing);
                    vm.ServiceProviderListVM.AvailableIcons.Insert(0, uploadedIcon);
                    vm.ServiceProviderListVM.SelectedIcon = uploadedIcon;
                    System.Diagnostics.Debug.WriteLine($"[SVG Upload] Icon added and selected. AvailableIcons count: {vm.ServiceProviderListVM.AvailableIcons.Count}");
                    return;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SVG Upload] FAILED: SVG content is empty");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SVG Upload] FAILED: IsValidSvg returned false");
            }
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[SVG Upload] No file selected (cancelled)");
        }

        var noIcon = vm.ServiceProviderListVM.AvailableIcons.FirstOrDefault(i => i.SvgContent == null && !i.IsUploadOption);
        vm.ServiceProviderListVM.SelectedIcon = noIcon;
    }

    private string GetResource(string key)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
            return s;
        return key;
    }

    private async void OnServiceProviderEditClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        ServiceProviderItemViewModel? item = null;

        if (sender is Button button)
            item = button.DataContext as ServiceProviderItemViewModel;
        else if (sender is MenuItem menuItem)
            item = menuItem.DataContext as ServiceProviderItemViewModel;

        if (item == null) return;

        var icons = vm.ServiceProviderListVM.AvailableIcons.ToList();
        var dialog = new ServiceProviderSettingsDialog(
            item.Name,
            item.Description,
            item.IconPath,
            item.IconColor,
            icons);
        var result = await dialog.ShowDialog<ServiceProviderSettingsResult?>(this);

        if (result != null)
        {
            await vm.ServiceProviderListVM.UpdateProviderAsync(item, result.Name, result.Description, result.IconPath, result.IconColor);
        }
    }

    private async void OnServiceProviderDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;
        ServiceProviderItemViewModel? item = null;

        if (sender is Button button)
            item = button.DataContext as ServiceProviderItemViewModel;
        else if (sender is MenuItem menuItem)
            item = menuItem.DataContext as ServiceProviderItemViewModel;

        if (item == null) return;

        var title = "Delete Provider";
        var message = $"Are you sure you want to delete provider \"{item.Name}\"?";

        if (Application.Current?.TryGetResource("Lang.ServiceProvider.DeleteConfirmTitle", null, out var t) == true && t is string tStr) title = tStr;
        if (Application.Current?.TryGetResource("Lang.ServiceProvider.DeleteConfirmMessage", null, out var m) == true && m is string mStr)
            message = string.Format(mStr, item.Name);

        var dialog = new ConfirmDialog(title, message);
        var result = await dialog.ShowDialog<bool>(this);

        if (result)
        {
            await vm.ServiceProviderListVM.DeleteProviderAsync(item);
        }
    }

    private async void OnExportAccountClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control || DataContext is not MainViewModel vm) return;
        if (control.DataContext is not AccountItemViewModel accountVm) return;

        var accounts = new List<Account> { accountVm.Account };
        var dialog = new ExportAccountDialog(accounts);
        await dialog.ShowDialog(this);
    }

    private async void OnBatchExportAccountClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var selectedAccounts = vm.AccountListVM.Accounts
            .Where(a => a.IsSelected)
            .Select(a => a.Account)
            .ToList();

        if (selectedAccounts.Count == 0)
        {
            var title = "Export Account";
            var message = "Please select at least one account";
            
            if (Application.Current?.TryGetResource("Lang.Export.Title", null, out var t) == true && t is string tStr) title = tStr;
            if (Application.Current?.TryGetResource("Lang.Export.NoAccountSelected", null, out var m) == true && m is string mStr) message = mStr;
            
            await ShowMessageAsync(title, message);
            return;
        }

        var dialog = new ExportAccountDialog(selectedAccounts);
        await dialog.ShowDialog(this);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var confirmText = "Confirm";
        if (Application.Current?.TryGetResource("Lang.Confirm", null, out var c) == true && c is string cStr) confirmText = cStr;

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var button = new Button
        {
            Content = confirmText,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        button.Click += (s, e) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 20,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                button
            }
        };

        await dialog.ShowDialog(this);
    }
}
