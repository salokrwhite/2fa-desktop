using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Services;

public interface IStorageService
{
    Task InitializeAsync();
    Task<IEnumerable<Account>> GetAllAccountsAsync();
    Task<Account?> GetAccountByIdAsync(Guid id);
    Task AddAccountAsync(Account account);
    Task UpdateAccountAsync(Account account);
    Task DeleteAccountAsync(Guid id);
    Task<bool> ExportDataAsync(string filePath, string password);
    Task<bool> ImportDataAsync(string filePath, string password);
    Task<Settings> GetSettingsAsync();
    Task UpdateSettingsAsync(Settings settings);
    Task RotateAccountSecretsAsync(string oldPassword, string newPassword);
}
