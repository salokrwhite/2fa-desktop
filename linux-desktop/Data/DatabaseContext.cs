using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace TwoFactorAuth.Data;

public sealed class DatabaseContext
{
    private const string DatabaseFileName = "TwoFactorAuth.db";

    public string DatabasePath { get; }

    public DatabaseContext()
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TwoFactorAuth-Desktop");
        Directory.CreateDirectory(root);
        DatabasePath = Path.Combine(root, DatabaseFileName);
    }

    public SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={DatabasePath}");
    }

    public async Task InitializeAsync()
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                CREATE TABLE IF NOT EXISTS Accounts (
                    Id              TEXT PRIMARY KEY,
                    Name            TEXT NOT NULL,
                    Issuer          TEXT,
                    Secret          TEXT NOT NULL,
                    Type            TEXT NOT NULL DEFAULT 'TOTP',
                    Digits          INTEGER NOT NULL DEFAULT 6,
                    Period          INTEGER NOT NULL DEFAULT 30,
                    Counter         INTEGER NOT NULL DEFAULT 0,
                    SortOrder       INTEGER NOT NULL DEFAULT 0,
                    CreatedAt       TEXT NOT NULL,
                    UpdatedAt       TEXT NOT NULL,
                    GroupName       TEXT,
                    Icon            TEXT,
                    IsFavorite      INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS Settings (
                    Key             TEXT PRIMARY KEY,
                    Value           TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS idx_accounts_name ON Accounts(Name);
                CREATE INDEX IF NOT EXISTS idx_accounts_issuer ON Accounts(Issuer);
                CREATE INDEX IF NOT EXISTS idx_accounts_group ON Accounts(GroupName);
                CREATE INDEX IF NOT EXISTS idx_accounts_favorite ON Accounts(IsFavorite);
                CREATE TABLE IF NOT EXISTS Categories (
                    Id              TEXT PRIMARY KEY,
                    Name            TEXT NOT NULL,
                    Description     TEXT NOT NULL DEFAULT '',
                    SortOrder       INTEGER NOT NULL DEFAULT 0
                );
                CREATE TABLE IF NOT EXISTS OperationLogs (
                    Id              TEXT PRIMARY KEY,
                    Timestamp       TEXT NOT NULL,
                    Operation       TEXT NOT NULL,
                    Target          TEXT,
                    Details         TEXT
                );
                CREATE TABLE IF NOT EXISTS ServiceProviders (
                    Id              TEXT PRIMARY KEY,
                    Name            TEXT NOT NULL,
                    IconPath        TEXT,
                    IconColor       TEXT,
                    Description     TEXT,
                    SortOrder       INTEGER NOT NULL DEFAULT 0,
                    CreatedAt       TEXT NOT NULL,
                    IsBuiltIn       INTEGER NOT NULL DEFAULT 0
                );
                """;
            await command.ExecuteNonQueryAsync();
        }
        await MigrateAddDescriptionColumnAsync(connection);
        await MigrateAddIconColorColumnAsync(connection);
    }

    private async Task MigrateAddDescriptionColumnAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Categories)";
        await using var reader = await command.ExecuteReaderAsync();

        bool hasDescriptionColumn = false;
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(1);
            if (columnName.Equals("Description", StringComparison.OrdinalIgnoreCase))
            {
                hasDescriptionColumn = true;
                break;
            }
        }
        await reader.CloseAsync();

        if (!hasDescriptionColumn)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE Categories ADD COLUMN Description TEXT NOT NULL DEFAULT ''";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }

    private async Task MigrateAddIconColorColumnAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(ServiceProviders)";
        await using var reader = await command.ExecuteReaderAsync();

        bool hasIconColorColumn = false;
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(1);
            if (columnName.Equals("IconColor", StringComparison.OrdinalIgnoreCase))
            {
                hasIconColorColumn = true;
                break;
            }
        }
        await reader.CloseAsync();

        if (!hasIconColorColumn)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE ServiceProviders ADD COLUMN IconColor TEXT";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }
}
