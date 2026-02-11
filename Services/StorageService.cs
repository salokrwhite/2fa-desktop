using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.Services;

[JsonSerializable(typeof(BackupPayload))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal partial class AppJsonContext : JsonSerializerContext
{
}

internal sealed class BackupPayload
{
    public List<Account> Accounts { get; init; } = new();
    public Settings Settings { get; init; } = new();
}

public sealed class StorageService : IStorageService
{
    private readonly DatabaseContext _dbContext;
    private readonly AccountRepository _accountRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly OperationLogRepository _operationLogRepository;
    private readonly ISecurityService _securityService;

    public StorageService(DatabaseContext dbContext, AccountRepository accountRepository, SettingsRepository settingsRepository, OperationLogRepository operationLogRepository, ISecurityService securityService)
    {
        _dbContext = dbContext;
        _accountRepository = accountRepository;
        _settingsRepository = settingsRepository;
        _operationLogRepository = operationLogRepository;
        _securityService = securityService;
    }

    public Task InitializeAsync()
    {
        return _dbContext.InitializeAsync();
    }

    public async Task<IEnumerable<Account>> GetAllAccountsAsync()
    {
        EnsureUnlocked();
        var accounts = await _accountRepository.GetAllAsync();
        foreach (var account in accounts)
        {
            account.Secret = await DecryptSecretWithFallbackAsync(account);
        }
        return accounts;
    }

    public async Task<Account?> GetAccountByIdAsync(Guid id)
    {
        EnsureUnlocked();
        var account = await _accountRepository.GetByIdAsync(id);
        if (account == null) return null;
        account.Secret = await DecryptSecretWithFallbackAsync(account);
        return account;
    }

    public async Task AddAccountAsync(Account account)
    {
        EnsureUnlocked();
        account.CreatedAt = account.CreatedAt == default ? DateTime.UtcNow : account.CreatedAt;
        account.UpdatedAt = DateTime.UtcNow;
        var encrypted = _securityService.EncryptWithSession(account.Secret);
        var stored = Clone(account, encrypted);
        await _accountRepository.AddAsync(stored);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.add_account",
            Target = GetAccountTarget(account),
            Details = GetAccountDetails(account)
        });
    }

    public async Task UpdateAccountAsync(Account account)
    {
        EnsureUnlocked();
        account.UpdatedAt = DateTime.UtcNow;
        var encrypted = _securityService.EncryptWithSession(account.Secret);
        var stored = Clone(account, encrypted);
        await _accountRepository.UpdateAsync(stored);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.update_account",
            Target = GetAccountTarget(account),
            Details = GetAccountDetails(account)
        });
    }

    public async Task DeleteAccountAsync(Guid id)
    {
        EnsureUnlocked();
        var existing = await _accountRepository.GetByIdAsync(id);
        await _accountRepository.DeleteAsync(id);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.delete_account",
            Target = existing == null ? id.ToString() : GetAccountTarget(existing),
            Details = existing == null ? string.Empty : GetAccountDetails(existing)
        });
    }

    public async Task<bool> ExportDataAsync(string filePath, string password)
    {
        EnsureUnlocked();
        var accounts = (await GetAllAccountsAsync()).ToList();
        var settings = await GetSettingsAsync();
        var payload = new BackupPayload
        {
            Accounts = accounts,
            Settings = settings
        };
        var json = JsonSerializer.Serialize(payload, AppJsonContext.Default.BackupPayload);
        var encrypted = _securityService.Encrypt(json, password);
        await File.WriteAllTextAsync(filePath, encrypted);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.export_backup",
            Target = Path.GetFileName(filePath),
            Details = $"Accounts={accounts.Count}"
        });
        return true;
    }

    public async Task<bool> ImportDataAsync(string filePath, string password)
    {
        EnsureUnlocked();
        var encrypted = await File.ReadAllTextAsync(filePath);
        var json = _securityService.Decrypt(encrypted, password);
        var payload = JsonSerializer.Deserialize(json, AppJsonContext.Default.BackupPayload);
        if (payload == null) return false;
        await _accountRepository.DeleteAllAsync();
        foreach (var account in payload.Accounts)
        {
            await AddAccountAsync(account);
        }
        await UpdateSettingsAsync(payload.Settings);
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.import_backup",
            Target = Path.GetFileName(filePath),
            Details = $"Accounts={payload.Accounts.Count}"
        });
        return true;
    }

    public async Task<Settings> GetSettingsAsync()
    {
        var values = await _settingsRepository.GetAllAsync();
        var settings = new Settings();
        if (values.TryGetValue(nameof(Settings.Theme), out var theme)) settings.Theme = theme;
        if (values.TryGetValue(nameof(Settings.Language), out var language)) settings.Language = language;
        if (values.TryGetValue(nameof(Settings.RefreshPeriod), out var refresh) && int.TryParse(refresh, out var r)) settings.RefreshPeriod = r;
        if (values.TryGetValue(nameof(Settings.AutoLockMinutes), out var autoLock) && int.TryParse(autoLock, out var a)) settings.AutoLockMinutes = a;
        return settings;
    }

    public async Task UpdateSettingsAsync(Settings settings)
    {
        await _settingsRepository.SetValueAsync(nameof(Settings.Theme), settings.Theme);
        await _settingsRepository.SetValueAsync(nameof(Settings.Language), settings.Language);
        await _settingsRepository.SetValueAsync(nameof(Settings.RefreshPeriod), settings.RefreshPeriod.ToString());
        await _settingsRepository.SetValueAsync(nameof(Settings.AutoLockMinutes), settings.AutoLockMinutes.ToString());
        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.update_settings",
            Target = "Settings",
            Details = $"Theme={settings.Theme}; Language={settings.Language}; RefreshPeriod={settings.RefreshPeriod}; AutoLockMinutes={settings.AutoLockMinutes}"
        });
    }

    public async Task RotateAccountSecretsAsync(string oldPassword, string newPassword)
    {
        EnsureUnlocked();

        var accounts = await _accountRepository.GetAllAsync();
        foreach (var account in accounts)
        {
            string plain;
            var secret = account.Secret;

            if (string.IsNullOrWhiteSpace(secret))
                continue;

            if (!IsEncryptedFormat(secret))
            {
                plain = secret;
            }
            else
            {
                plain = DecryptForRotation(secret, oldPassword);
            }

            var encrypted = string.IsNullOrEmpty(newPassword)
                ? _securityService.Encrypt(plain, string.Empty)
                : _securityService.Encrypt(plain, newPassword);
            account.Secret = encrypted;
            account.UpdatedAt = DateTime.UtcNow;
            await _accountRepository.UpdateAsync(account);
        }

        await _operationLogRepository.AddAsync(new OperationLog
        {
            Operation = "op.update_settings",
            Target = "Security",
            Details = "RotateAccountSecrets"
        });
    }

    private string DecryptForRotation(string secret, string oldPassword)
    {
        try
        {
            return _securityService.Decrypt(secret, oldPassword);
        }
        catch {  }

        try
        {
            return _securityService.DecryptWithSession(secret);
        }
        catch {  }

        try
        {
            return _securityService.Decrypt(secret, string.Empty);
        }
        catch {  }

        return secret;
    }

    private void EnsureUnlocked()
    {
        if (!_securityService.IsUnlocked)
        {
            throw new InvalidOperationException("Locked");
        }
    }

    private async Task<string> DecryptSecretWithFallbackAsync(Account account)
    {
        var secret = account.Secret;
        if (string.IsNullOrWhiteSpace(secret)) return string.Empty;
        if (!IsEncryptedFormat(secret)) return secret;

        try
        {
            return _securityService.DecryptWithSession(secret);
        }
        catch (AuthenticationTagMismatchException)
        {
            
        }
        catch (FormatException)
        {
            
        }
        catch (CryptographicException)
        {
            
        }

        try
        {
            var plain = _securityService.Decrypt(secret, string.Empty);
            var encrypted = _securityService.EncryptWithSession(plain);
            account.Secret = encrypted;
            account.UpdatedAt = DateTime.UtcNow;
            await _accountRepository.UpdateAsync(account);
            return plain;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsEncryptedFormat(string secret)
    {
        var parts = secret.Split(':');
        return parts.Length == 4;
    }

    private static string GetAccountTarget(Account account)
    {
        if (!string.IsNullOrWhiteSpace(account.Issuer) && !string.IsNullOrWhiteSpace(account.Name))
        {
            return $"{account.Issuer} / {account.Name}";
        }
        if (!string.IsNullOrWhiteSpace(account.Name)) return account.Name;
        if (!string.IsNullOrWhiteSpace(account.Issuer)) return account.Issuer;
        return account.Id.ToString();
    }

    private static string GetAccountDetails(Account account)
    {
        var group = string.IsNullOrWhiteSpace(account.Group) ? "-" : account.Group;
        return $"Id={account.Id}; Type={account.Type}; Digits={account.Digits}; Period={account.Period}; Group={group}; Favorite={account.IsFavorite}";
    }

    private static Account Clone(Account account, string encryptedSecret)
    {
        return new Account
        {
            Id = account.Id,
            Name = account.Name,
            Issuer = account.Issuer,
            Secret = encryptedSecret,
            Type = account.Type,
            Digits = account.Digits,
            Period = account.Period,
            Counter = account.Counter,
            SortOrder = account.SortOrder,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt,
            Group = account.Group,
            Icon = account.Icon,
            IsFavorite = account.IsFavorite
        };
    }
}
