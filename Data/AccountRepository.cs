using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Data;

public sealed class AccountRepository
{
    private readonly DatabaseContext _context;

    public AccountRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<List<Account>> GetAllAsync()
    {
        var accounts = new List<Account>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Issuer, Secret, Type, Digits, Period, Counter, SortOrder, CreatedAt, UpdatedAt, GroupName, Icon, IsFavorite FROM Accounts ORDER BY SortOrder, Name";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            accounts.Add(ReadAccount(reader));
        }
        return accounts;
    }

    public async Task<int> GetCountAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Accounts";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<Account?> GetByIdAsync(Guid id)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Issuer, Secret, Type, Digits, Period, Counter, SortOrder, CreatedAt, UpdatedAt, GroupName, Icon, IsFavorite FROM Accounts WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return ReadAccount(reader);
        }
        return null;
    }

    public async Task AddAsync(Account account)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Accounts (Id, Name, Issuer, Secret, Type, Digits, Period, Counter, SortOrder, CreatedAt, UpdatedAt, GroupName, Icon, IsFavorite)
            VALUES ($id, $name, $issuer, $secret, $type, $digits, $period, $counter, $sortOrder, $createdAt, $updatedAt, $groupName, $icon, $isFavorite)
            """;
        BindParameters(command, account);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(Account account)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Accounts SET
                Name = $name,
                Issuer = $issuer,
                Secret = $secret,
                Type = $type,
                Digits = $digits,
                Period = $period,
                Counter = $counter,
                SortOrder = $sortOrder,
                CreatedAt = $createdAt,
                UpdatedAt = $updatedAt,
                GroupName = $groupName,
                Icon = $icon,
                IsFavorite = $isFavorite
            WHERE Id = $id
            """;
        BindParameters(command, account);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Accounts WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAllAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Accounts";
        await command.ExecuteNonQueryAsync();
    }

    private static void BindParameters(SqliteCommand command, Account account)
    {
        command.Parameters.AddWithValue("$id", account.Id.ToString());
        command.Parameters.AddWithValue("$name", account.Name);
        command.Parameters.AddWithValue("$issuer", string.IsNullOrWhiteSpace(account.Issuer) ? DBNull.Value : account.Issuer);
        command.Parameters.AddWithValue("$secret", account.Secret);
        command.Parameters.AddWithValue("$type", account.Type == OtpType.Hotp ? "HOTP" : "TOTP");
        command.Parameters.AddWithValue("$digits", account.Digits);
        command.Parameters.AddWithValue("$period", account.Period);
        command.Parameters.AddWithValue("$counter", account.Counter);
        command.Parameters.AddWithValue("$sortOrder", account.SortOrder);
        command.Parameters.AddWithValue("$createdAt", account.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$updatedAt", account.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$groupName", string.IsNullOrWhiteSpace(account.Group) ? DBNull.Value : account.Group);
        command.Parameters.AddWithValue("$icon", string.IsNullOrWhiteSpace(account.Icon) ? DBNull.Value : account.Icon);
        command.Parameters.AddWithValue("$isFavorite", account.IsFavorite ? 1 : 0);
    }

    private static Account ReadAccount(SqliteDataReader reader)
    {
        var typeText = reader.GetString(4);
        return new Account
        {
            Id = Guid.Parse(reader.GetString(0)),
            Name = reader.GetString(1),
            Issuer = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Secret = reader.GetString(3),
            Type = typeText.Equals("HOTP", StringComparison.OrdinalIgnoreCase) ? OtpType.Hotp : OtpType.Totp,
            Digits = reader.GetInt32(5),
            Period = reader.GetInt32(6),
            Counter = reader.GetInt32(7),
            SortOrder = reader.GetInt32(8),
            CreatedAt = DateTime.Parse(reader.GetString(9)),
            UpdatedAt = DateTime.Parse(reader.GetString(10)),
            Group = reader.IsDBNull(11) ? null : reader.GetString(11),
            Icon = reader.IsDBNull(12) ? null : reader.GetString(12),
            IsFavorite = reader.GetInt32(13) == 1
        };
    }
}
