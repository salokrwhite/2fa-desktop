using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace TwoFactorAuth.Data;

public sealed class SettingsRepository
{
    private readonly DatabaseContext _context;
    private Dictionary<string, string>? _cache;

    public SettingsRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task PreloadAsync()
    {
        _cache = await GetAllFromDbAsync();
    }

    public async Task<string?> GetValueAsync(string key)
    {
        if (_cache != null)
        {
            return _cache.TryGetValue(key, out var cached) ? cached : null;
        }

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = $key";
        command.Parameters.AddWithValue("$key", key);
        var result = await command.ExecuteScalarAsync();
        return result == null ? null : Convert.ToString(result);
    }

    public async Task SetValueAsync(string key, string value)
    {
        _cache?.Remove(key);
        _cache?.Add(key, value);

        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ($key, $value)
            ON CONFLICT(Key) DO UPDATE SET Value = $value
            """;
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<string, string>> GetAllAsync()
    {
        if (_cache != null)
        {
            return new Dictionary<string, string>(_cache, StringComparer.OrdinalIgnoreCase);
        }
        return await GetAllFromDbAsync();
    }

    private async Task<Dictionary<string, string>> GetAllFromDbAsync()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Key, Value FROM Settings";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result[reader.GetString(0)] = reader.GetString(1);
        }
        return result;
    }
}
