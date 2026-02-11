using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using TwoFactorAuth.Data;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Services;

public sealed class BackupService
{
    private readonly AccountRepository _accountRepository;
    private readonly CategoryRepository _categoryRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly OperationLogRepository _operationLogRepository;
    private readonly ISecurityService _securityService;

    private const int BackupKeyIterations = 200000; 
    private const string BackupFileExtension = ".2fabackup";

    public BackupService(
        AccountRepository accountRepository,
        CategoryRepository categoryRepository,
        SettingsRepository settingsRepository,
        OperationLogRepository operationLogRepository,
        ISecurityService securityService)
    {
        _accountRepository = accountRepository;
        _categoryRepository = categoryRepository;
        _settingsRepository = settingsRepository;
        _operationLogRepository = operationLogRepository;
        _securityService = securityService;
    }

    public async Task<BackupFile> ExportAsync(string backupPassword, bool includeSettings, bool includeLogs)
    {
        var accounts = await _accountRepository.GetAllAsync();
        var categories = await _categoryRepository.GetAllAsync();

        var backupData = new BackupData
        {
            Accounts = accounts.Select(a => new BackupAccount
            {
                Id = a.Id,
                Name = a.Name,
                Issuer = a.Issuer,
                Secret = a.Secret,
                Type = a.Type == OtpType.Hotp ? "HOTP" : "TOTP",
                Digits = a.Digits,
                Period = a.Period,
                Counter = a.Counter,
                SortOrder = a.SortOrder,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                GroupName = a.Group,
                Icon = a.Icon,
                IsFavorite = a.IsFavorite
            }).ToList(),
            Categories = categories.Select(c => new BackupCategory
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                SortOrder = c.SortOrder
            }).ToList()
        };

        if (includeSettings)
        {
            var allSettings = await _settingsRepository.GetAllAsync();
            backupData.Settings = allSettings.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        if (includeLogs)
        {
            var logs = await _operationLogRepository.GetAllAsync();
            backupData.OperationLogs = logs.Select(log => new BackupOperationLog
            {
                Id = log.Id,
                Timestamp = log.Timestamp,
                Operation = log.Operation,
                Target = log.Target,
                Details = log.Details
            }).ToList();
        }

        var jsonData = JsonSerializer.Serialize(backupData, BackupJsonContext.Default.BackupData);
        var encryptedData = EncryptBackupData(jsonData, backupPassword);
        var signature = GenerateSignature(encryptedData, backupPassword);
        var backupFile = new BackupFile
        {
            Version = "1.0",
            Timestamp = DateTime.UtcNow,
            AppVersion = "1.0.0",
            Metadata = new BackupMetadata
            {
                AccountCount = backupData.Accounts.Count,
                CategoryCount = backupData.Categories.Count,
                HasSettings = includeSettings && backupData.Settings.Count > 0,
                HasLogs = includeLogs && backupData.OperationLogs.Count > 0
            },
            EncryptedData = encryptedData,
            Signature = signature
        };

        return backupFile;
    }

    public async Task<ImportResult> ImportAsync(BackupFile backupFile, string backupPassword, ImportMode mode, ConflictStrategy conflictStrategy)
    {
        var result = new ImportResult();

        try
        {
            var expectedSignature = GenerateSignature(backupFile.EncryptedData, backupPassword);
            if (expectedSignature != backupFile.Signature)
            {
                result.Success = false;
                result.ErrorMessage = GetLang("Lang.Backup.Import.SignatureError", "数据签名验证失败，文件可能已被篡改或密码错误");
                return result;
            }

            string jsonData;
            try
            {
                jsonData = DecryptBackupData(backupFile.EncryptedData, backupPassword);
            }
            catch (CryptographicException)
            {
                result.Success = false;
                result.ErrorMessage = GetLang("Lang.Backup.Import.DecryptError", "解密失败，密码错误");
                return result;
            }

            var backupData = JsonSerializer.Deserialize(jsonData, BackupJsonContext.Default.BackupData);

            if (backupData == null)
            {
                result.Success = false;
                result.ErrorMessage = GetLang("Lang.Backup.Import.DataFormatError", "备份数据格式错误");
                return result;
            }

            if (mode == ImportMode.Overwrite)
            {
                await _accountRepository.DeleteAllAsync();
                result.AccountsDeleted = (await _accountRepository.GetAllAsync()).Count;
            }

            var existingAccounts = await _accountRepository.GetAllAsync();
            var existingSecrets = existingAccounts.Select(a => a.Secret).ToHashSet();

            foreach (var backupAccount in backupData.Accounts)
            {
                if (mode == ImportMode.Merge && existingSecrets.Contains(backupAccount.Secret))
                {
                    if (conflictStrategy == ConflictStrategy.Skip)
                    {
                        result.AccountsSkipped++;
                        continue;
                    }
                    else if (conflictStrategy == ConflictStrategy.Rename)
                    {
                        backupAccount.Name += GetLang("Lang.Backup.Import.RenameSuffix", " (导入)");
                        backupAccount.Id = Guid.NewGuid();
                    }
                    else if (conflictStrategy == ConflictStrategy.Overwrite)
                    {
                        var existing = existingAccounts.FirstOrDefault(a => a.Secret == backupAccount.Secret);
                        if (existing != null)
                        {
                            backupAccount.Id = existing.Id;
                            await _accountRepository.UpdateAsync(ConvertToAccount(backupAccount));
                            result.AccountsUpdated++;
                            continue;
                        }
                    }
                }

                await _accountRepository.AddAsync(ConvertToAccount(backupAccount));
                result.AccountsImported++;
            }

            var existingCategories = await _categoryRepository.GetAllAsync();
            var existingCategoryNames = existingCategories.Select(c => c.Name).ToHashSet();

            foreach (var backupCategory in backupData.Categories)
            {
                if (mode == ImportMode.Merge && existingCategoryNames.Contains(backupCategory.Name))
                {
                    if (conflictStrategy == ConflictStrategy.Skip)
                    {
                        result.CategoriesSkipped++;
                        continue;
                    }
                    else if (conflictStrategy == ConflictStrategy.Rename)
                    {
                        backupCategory.Name += GetLang("Lang.Backup.Import.RenameSuffix", " (导入)");
                        backupCategory.Id = Guid.NewGuid();
                    }
                }

                await _categoryRepository.AddAsync(new Category
                {
                    Id = backupCategory.Id,
                    Name = backupCategory.Name,
                    Description = backupCategory.Description,
                    SortOrder = backupCategory.SortOrder
                });
                result.CategoriesImported++;
            }

            if (backupData.Settings.Count > 0)
            {
                foreach (var setting in backupData.Settings)
                {
                    if (setting.Key != SettingKeys.MasterPasswordHash)
                    {
                        await _settingsRepository.SetValueAsync(setting.Key, setting.Value);
                    }
                }
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = string.Format(GetLang("Lang.Backup.Import.FailedFormat", "导入失败: {0}"), ex.Message);
        }

        return result;
    }

    public bool ValidateBackupFile(BackupFile backupFile)
    {
        if (backupFile == null) return false;
        if (string.IsNullOrWhiteSpace(backupFile.Version)) return false;
        if (string.IsNullOrWhiteSpace(backupFile.EncryptedData)) return false;
        if (string.IsNullOrWhiteSpace(backupFile.Signature)) return false;
        return true;
    }

    public string GenerateBackupFileName()
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"2FA_Backup_{timestamp}{BackupFileExtension}";
    }

    private string EncryptBackupData(string plainText, string password)
    {
        var salt = _securityService.GenerateSalt();
        var key = _securityService.DeriveKey(password, salt, BackupKeyIterations);
        var iv = RandomNumberGenerator.GetBytes(12);

        using var aes = new AesGcm(key, 16);
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        aes.Encrypt(iv, plaintextBytes, cipher, tag);

        return Convert.ToBase64String(salt) + ":" +
               Convert.ToBase64String(iv) + ":" +
               Convert.ToBase64String(cipher) + ":" +
               Convert.ToBase64String(tag);
    }

    private string DecryptBackupData(string cipherText, string password)
    {
        var parts = cipherText.Split(':');
        if (parts.Length != 4)
            throw new CryptographicException("Invalid backup format");

        var salt = Convert.FromBase64String(parts[0]);
        var iv = Convert.FromBase64String(parts[1]);
        var cipher = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);

        var key = _securityService.DeriveKey(password, salt, BackupKeyIterations);

        using var aes = new AesGcm(key, 16);
        var plaintext = new byte[cipher.Length];
        aes.Decrypt(iv, cipher, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    private string GenerateSignature(string data, string password)
    {
        var keyBytes = Encoding.UTF8.GetBytes(password);
        using var hmac = new HMACSHA256(keyBytes);
        var dataBytes = Encoding.UTF8.GetBytes(data);
        var hash = hmac.ComputeHash(dataBytes);
        return Convert.ToBase64String(hash);
    }

    private Account ConvertToAccount(BackupAccount backupAccount)
    {
        return new Account
        {
            Id = backupAccount.Id,
            Name = backupAccount.Name,
            Issuer = backupAccount.Issuer,
            Secret = backupAccount.Secret,
            Type = backupAccount.Type.Equals("HOTP", StringComparison.OrdinalIgnoreCase) ? OtpType.Hotp : OtpType.Totp,
            Digits = backupAccount.Digits,
            Period = backupAccount.Period,
            Counter = backupAccount.Counter,
            SortOrder = backupAccount.SortOrder,
            CreatedAt = backupAccount.CreatedAt,
            UpdatedAt = backupAccount.UpdatedAt,
            Group = backupAccount.GroupName,
            Icon = backupAccount.Icon,
            IsFavorite = backupAccount.IsFavorite
        };
    }

    private static string GetLang(string key, string fallback)
    {
        if (Application.Current?.TryGetResource(key, null, out var res) == true && res is string s)
            return s;
        return fallback;
    }
}

public enum ImportMode
{
    Merge,      
    Overwrite   
}

public enum ConflictStrategy
{
    Skip,       
    Overwrite,  
    Rename      
}

public sealed class ImportResult
{
    public bool Success { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public int AccountsImported { get; set; }
    public int AccountsUpdated { get; set; }
    public int AccountsSkipped { get; set; }
    public int AccountsDeleted { get; set; }
    public int CategoriesImported { get; set; }
    public int CategoriesSkipped { get; set; }
}
