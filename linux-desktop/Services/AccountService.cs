using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwoFactorAuth.Models;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.Services;

public sealed class AccountService : IAccountService
{
    private readonly IStorageService _storageService;

    public AccountService(IStorageService storageService)
    {
        _storageService = storageService;
    }

    public Task<IEnumerable<Account>> GetAllAccountsAsync()
    {
        return _storageService.GetAllAccountsAsync();
    }

    public Task<Account?> GetAccountByIdAsync(Guid id)
    {
        return _storageService.GetAccountByIdAsync(id);
    }

    public Task AddAccountAsync(Account account)
    {
        return _storageService.AddAccountAsync(account);
    }

    public Task UpdateAccountAsync(Account account)
    {
        return _storageService.UpdateAccountAsync(account);
    }

    public Task DeleteAccountAsync(Guid id)
    {
        return _storageService.DeleteAccountAsync(id);
    }
}
