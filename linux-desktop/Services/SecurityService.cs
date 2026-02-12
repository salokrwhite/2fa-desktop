using System;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BCrypt.Net;
using TwoFactorAuth.Data;
using TwoFactorAuth.Services;

namespace TwoFactorAuth.Services;

public sealed class SecurityService : ISecurityService
{
    private readonly SettingsRepository _settingsRepository;
    private volatile bool _unlocked;
    private string? _masterPasswordHash;
    private string? _sessionPassword;

    public SecurityService(SettingsRepository settingsRepository)
    {
        _settingsRepository = settingsRepository;
    }

    public bool IsUnlocked => _unlocked;

    public byte[] DeriveKey(string password, byte[] salt, int iterations = 100000)
    {
        return Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
    }

    public string Encrypt(string plainText, string password)
    {
        var salt = GenerateSalt();
        var key = DeriveKey(password, salt);
        var iv = RandomNumberGenerator.GetBytes(12);
        using var aes = new AesGcm(key, 16);
        var plaintextBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = new byte[plaintextBytes.Length];
        var tag = new byte[16];
        aes.Encrypt(iv, plaintextBytes, cipher, tag);
        var result = Convert.ToBase64String(salt) + ":" + Convert.ToBase64String(iv) + ":" + Convert.ToBase64String(cipher) + ":" + Convert.ToBase64String(tag);
        return result;
    }

    public string Decrypt(string cipherText, string password)
    {
        var parts = cipherText.Split(':');
        var salt = Convert.FromBase64String(parts[0]);
        var iv = Convert.FromBase64String(parts[1]);
        var cipher = Convert.FromBase64String(parts[2]);
        var tag = Convert.FromBase64String(parts[3]);
        var key = DeriveKey(password, salt);
        using var aes = new AesGcm(key, 16);
        var plaintext = new byte[cipher.Length];
        aes.Decrypt(iv, cipher, tag, plaintext);
        return Encoding.UTF8.GetString(plaintext);
    }

    public byte[] GenerateSalt()
    {
        return RandomNumberGenerator.GetBytes(16);
    }

    public bool VerifyPassword(string password, string storedHash)
    {
        return BCrypt.Net.BCrypt.Verify(password, storedHash);
    }

    public string EncryptWithSession(string plainText)
    {
        if (!_unlocked) throw new InvalidOperationException("Locked");
        return Encrypt(plainText, _sessionPassword ?? string.Empty);
    }

    public string DecryptWithSession(string cipherText)
    {
        if (!_unlocked) throw new InvalidOperationException("Locked");
        return Decrypt(cipherText, _sessionPassword ?? string.Empty);
    }

    public async Task<bool> UnlockAsync(string password)
    {
        if (_masterPasswordHash == null)
        {
            _masterPasswordHash = await _settingsRepository.GetValueAsync(SettingKeys.MasterPasswordHash);
        }

        if (string.IsNullOrWhiteSpace(_masterPasswordHash))
        {
            _sessionPassword = string.Empty;
            _unlocked = true;
            return true;
        }

        var ok = BCrypt.Net.BCrypt.Verify(password, _masterPasswordHash);
        if (ok)
        {
            _unlocked = true;
            _sessionPassword = password;
        }
        return ok;
    }

    public async Task UnlockOnStartupAsync()
    {
        // Load the master password hash so session password can be set later via UnlockAsync
        if (_masterPasswordHash == null)
        {
            _masterPasswordHash = await _settingsRepository.GetValueAsync(SettingKeys.MasterPasswordHash);
        }
        
        if (string.IsNullOrWhiteSpace(_masterPasswordHash))
        {
            _sessionPassword = string.Empty;
        }
        
        _unlocked = true;
    }

    public async Task<bool> HasMasterPasswordAsync()
    {
        if (_masterPasswordHash == null)
        {
            _masterPasswordHash = await _settingsRepository.GetValueAsync(SettingKeys.MasterPasswordHash);
        }

        return !string.IsNullOrWhiteSpace(_masterPasswordHash);
    }

    public async Task SetMasterPasswordAsync(string password)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        await _settingsRepository.SetValueAsync(SettingKeys.MasterPasswordHash, hash);
        _masterPasswordHash = hash;
        _sessionPassword = password;
        _unlocked = true;
    }

    public async Task ClearMasterPasswordAsync()
    {
        await _settingsRepository.SetValueAsync(SettingKeys.MasterPasswordHash, string.Empty);
        _masterPasswordHash = null;
        _sessionPassword = string.Empty;
        _unlocked = true;
    }

    public Task LockAsync()
    {
        _sessionPassword = null;
        _unlocked = false;
        return Task.CompletedTask;
    }

    public string? GetSessionPassword()
    {
        return _sessionPassword;
    }
}
