using System;
using System.Threading.Tasks;

namespace TwoFactorAuth.Services;

public interface ISecurityService
{
    byte[] DeriveKey(string password, byte[] salt, int iterations = 100000);
    string Encrypt(string plainText, string password);
    string Decrypt(string cipherText, string password);
    string EncryptWithSession(string plainText);
    string DecryptWithSession(string cipherText);
    byte[] GenerateSalt();
    bool VerifyPassword(string password, string storedHash);
    Task<bool> UnlockAsync(string password);
    Task UnlockOnStartupAsync();
    Task LockAsync();
    bool IsUnlocked { get; }
    Task<bool> HasMasterPasswordAsync();
    Task SetMasterPasswordAsync(string password);
    Task ClearMasterPasswordAsync();
    string? GetSessionPassword();
}
