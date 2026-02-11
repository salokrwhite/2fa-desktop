using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Services;

public interface IAccountService
{
    Task<IEnumerable<Account>> GetAllAccountsAsync();
    Task<Account?> GetAccountByIdAsync(Guid id);
    Task AddAccountAsync(Account account);
    Task UpdateAccountAsync(Account account);
    Task DeleteAccountAsync(Guid id);
}
