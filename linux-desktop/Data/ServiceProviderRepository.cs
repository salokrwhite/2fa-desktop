using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Data;

public sealed class ServiceProviderRepository
{
    private readonly DatabaseContext _context;

    public ServiceProviderRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<List<ServiceProvider>> GetAllAsync()
    {
        var list = new List<ServiceProvider>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, IconPath, Description, SortOrder, CreatedAt, IsBuiltIn, IconColor FROM ServiceProviders ORDER BY SortOrder, Name";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new ServiceProvider
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                IconPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                SortOrder = reader.GetInt32(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                IsBuiltIn = reader.GetInt32(6) == 1,
                IconColor = reader.IsDBNull(7) ? null : reader.GetString(7)
            });
        }
        return list;
    }

    public async Task<ServiceProvider?> GetByNameAsync(string name)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, IconPath, Description, SortOrder, CreatedAt, IsBuiltIn, IconColor FROM ServiceProviders WHERE Name = $name";
        command.Parameters.AddWithValue("$name", name);
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new ServiceProvider
            {
                Id = Guid.Parse(reader.GetString(0)),
                Name = reader.GetString(1),
                IconPath = reader.IsDBNull(2) ? null : reader.GetString(2),
                Description = reader.IsDBNull(3) ? null : reader.GetString(3),
                SortOrder = reader.GetInt32(4),
                CreatedAt = DateTime.Parse(reader.GetString(5)),
                IsBuiltIn = reader.GetInt32(6) == 1,
                IconColor = reader.IsDBNull(7) ? null : reader.GetString(7)
            };
        }
        return null;
    }

    public async Task AddAsync(ServiceProvider provider)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO ServiceProviders (Id, Name, IconPath, IconColor, Description, SortOrder, CreatedAt, IsBuiltIn)
            VALUES ($id, $name, $iconPath, $iconColor, $description, $sortOrder, $createdAt, $isBuiltIn)
            """;
        command.Parameters.AddWithValue("$id", provider.Id.ToString());
        command.Parameters.AddWithValue("$name", provider.Name);
        command.Parameters.AddWithValue("$iconPath", (object?)provider.IconPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$iconColor", (object?)provider.IconColor ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)provider.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", provider.SortOrder);
        command.Parameters.AddWithValue("$createdAt", provider.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$isBuiltIn", provider.IsBuiltIn ? 1 : 0);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateAsync(ServiceProvider provider)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE ServiceProviders SET Name = $name, IconPath = $iconPath, IconColor = $iconColor, Description = $description,
            SortOrder = $sortOrder, IsBuiltIn = $isBuiltIn WHERE Id = $id
            """;
        command.Parameters.AddWithValue("$id", provider.Id.ToString());
        command.Parameters.AddWithValue("$name", provider.Name);
        command.Parameters.AddWithValue("$iconPath", (object?)provider.IconPath ?? DBNull.Value);
        command.Parameters.AddWithValue("$iconColor", (object?)provider.IconColor ?? DBNull.Value);
        command.Parameters.AddWithValue("$description", (object?)provider.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("$sortOrder", provider.SortOrder);
        command.Parameters.AddWithValue("$isBuiltIn", provider.IsBuiltIn ? 1 : 0);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ServiceProviders WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync();
    }

    private const int BuiltInVersion = 1;

    public async Task InitializeBuiltInProvidersAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var checkCmd = connection.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM ServiceProviders WHERE IsBuiltIn = 1";
        var count = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
        if (count > 0)
        {
            await using var verCmd = connection.CreateCommand();
            verCmd.CommandText = "SELECT Value FROM Settings WHERE Key = 'BuiltInProviders.Version'";
            var verResult = await verCmd.ExecuteScalarAsync();
            var storedVersion = verResult != null ? Convert.ToInt32(verResult) : 0;
            if (storedVersion >= BuiltInVersion)
                return;

            await UpdateBuiltInColorsAsync(connection);

            await using var setVerCmd = connection.CreateCommand();
            setVerCmd.CommandText = """
                INSERT INTO Settings (Key, Value) VALUES ('BuiltInProviders.Version', $ver)
                ON CONFLICT(Key) DO UPDATE SET Value = $ver
                """;
            setVerCmd.Parameters.AddWithValue("$ver", BuiltInVersion.ToString());
            await setVerCmd.ExecuteNonQueryAsync();
            return;
        }

        var builtInProviders = BuiltInServiceProviders.All;

        for (int i = 0; i < builtInProviders.Length; i++)
        {
            var (name, svgContent, color) = builtInProviders[i];

            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO ServiceProviders (Id, Name, IconPath, IconColor, Description, SortOrder, CreatedAt, IsBuiltIn)
                VALUES ($id, $name, $iconPath, $iconColor, NULL, $sortOrder, $createdAt, 1)
                """;
            cmd.Parameters.AddWithValue("$id", Guid.NewGuid().ToString());
            cmd.Parameters.AddWithValue("$name", name);
            cmd.Parameters.AddWithValue("$iconPath", svgContent);
            cmd.Parameters.AddWithValue("$iconColor", color);
            cmd.Parameters.AddWithValue("$sortOrder", i);
            cmd.Parameters.AddWithValue("$createdAt", DateTime.UtcNow.ToString("O"));
            await cmd.ExecuteNonQueryAsync();
        }

        await using var saveVerCmd = connection.CreateCommand();
        saveVerCmd.CommandText = """
            INSERT INTO Settings (Key, Value) VALUES ('BuiltInProviders.Version', $ver)
            ON CONFLICT(Key) DO UPDATE SET Value = $ver
            """;
        saveVerCmd.Parameters.AddWithValue("$ver", BuiltInVersion.ToString());
        await saveVerCmd.ExecuteNonQueryAsync();
    }

    private async Task UpdateBuiltInColorsAsync(SqliteConnection connection)
    {
        foreach (var (name, svgContent, color) in BuiltInServiceProviders.All)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE ServiceProviders SET IconPath = $iconPath, IconColor = $color WHERE Name = $name AND IsBuiltIn = 1";
            cmd.Parameters.AddWithValue("$iconPath", svgContent);
            cmd.Parameters.AddWithValue("$color", color);
            cmd.Parameters.AddWithValue("$name", name);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
