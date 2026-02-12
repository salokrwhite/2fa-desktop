using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Data;

public sealed class OperationLogRepository
{
    private readonly DatabaseContext _context;

    public OperationLogRepository(DatabaseContext context)
    {
        _context = context;
    }

    public async Task<List<OperationLog>> GetAllAsync()
    {
        var list = new List<OperationLog>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var columns = await GetOperationLogsColumnsAsync(connection);
        var idCol = PickColumn(columns, "Id", "ID", "LogId", "LogID");
        var tsCol = PickColumn(columns, "Timestamp", "TimeStamp", "CreatedAt", "CreatedOn", "Time");
        var opCol = PickColumn(columns, "Operation", "Action", "Op");
        var targetCol = PickColumn(columns, "Target", "Object", "Entity");
        var detailsCol = PickColumn(columns, "Details", "Detail", "Message", "Description", "Desc");

        await using var command = connection.CreateCommand();
        var selectId = idCol == null ? "'' AS Id" : $"{idCol} AS Id";
        var selectTs = tsCol == null ? "'' AS Timestamp" : $"{tsCol} AS Timestamp";
        var selectOp = opCol == null ? "'' AS Operation" : $"{opCol} AS Operation";
        var selectTarget = targetCol == null ? "'' AS Target" : $"{targetCol} AS Target";
        var selectDetails = detailsCol == null ? "'' AS Details" : $"{detailsCol} AS Details";
        var orderBy = tsCol == null ? "rowid DESC" : "Timestamp DESC";
        command.CommandText = $"SELECT {selectId}, {selectTs}, {selectOp}, {selectTarget}, {selectDetails} FROM OperationLogs ORDER BY {orderBy} LIMIT 1000";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var idText = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var tsText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var opText = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var targetText = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var detailsText = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);

            list.Add(new OperationLog
            {
                Id = TryParseGuidOrEmpty(idText),
                Timestamp = ParseTimestampOrMin(tsText),
                Operation = opText,
                Target = targetText,
                Details = detailsText
            });
        }
        return list;
    }

    public async Task<int> GetCountAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM OperationLogs";
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<List<OperationLog>> GetRecentAsync(int count = 5)
    {
        var list = new List<OperationLog>();
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();

        var columns = await GetOperationLogsColumnsAsync(connection);
        var idCol = PickColumn(columns, "Id", "ID", "LogId", "LogID");
        var tsCol = PickColumn(columns, "Timestamp", "TimeStamp", "CreatedAt", "CreatedOn", "Time");
        var opCol = PickColumn(columns, "Operation", "Action", "Op");
        var targetCol = PickColumn(columns, "Target", "Object", "Entity");
        var detailsCol = PickColumn(columns, "Details", "Detail", "Message", "Description", "Desc");

        await using var command = connection.CreateCommand();
        var selectId = idCol == null ? "'' AS Id" : $"{idCol} AS Id";
        var selectTs = tsCol == null ? "'' AS Timestamp" : $"{tsCol} AS Timestamp";
        var selectOp = opCol == null ? "'' AS Operation" : $"{opCol} AS Operation";
        var selectTarget = targetCol == null ? "'' AS Target" : $"{targetCol} AS Target";
        var selectDetails = detailsCol == null ? "'' AS Details" : $"{detailsCol} AS Details";
        var orderBy = tsCol == null ? "rowid DESC" : "Timestamp DESC";
        command.CommandText = $"SELECT {selectId}, {selectTs}, {selectOp}, {selectTarget}, {selectDetails} FROM OperationLogs ORDER BY {orderBy} LIMIT {count}";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var idText = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
            var tsText = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
            var opText = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var targetText = reader.IsDBNull(3) ? string.Empty : reader.GetString(3);
            var detailsText = reader.IsDBNull(4) ? string.Empty : reader.GetString(4);

            list.Add(new OperationLog
            {
                Id = TryParseGuidOrEmpty(idText),
                Timestamp = ParseTimestampOrMin(tsText),
                Operation = opText,
                Target = targetText,
                Details = detailsText
            });
        }
        return list;
    }

    public async Task AddAsync(OperationLog log)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO OperationLogs (Id, Timestamp, Operation, Target, Details) VALUES ($id, $timestamp, $operation, $target, $details)";
        command.Parameters.AddWithValue("$id", log.Id.ToString());
        command.Parameters.AddWithValue("$timestamp", log.Timestamp.ToString("o"));
        command.Parameters.AddWithValue("$operation", log.Operation);
        command.Parameters.AddWithValue("$target", log.Target ?? string.Empty);
        command.Parameters.AddWithValue("$details", log.Details ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM OperationLogs WHERE Id = $id";
        command.Parameters.AddWithValue("$id", id.ToString());
        await command.ExecuteNonQueryAsync();
    }

    public async Task DeleteAllAsync()
    {
        await using var connection = _context.CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM OperationLogs";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<HashSet<string>> GetOperationLogsColumnsAsync(SqliteConnection connection)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(OperationLogs);";
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(1))
            {
                cols.Add(reader.GetString(1));
            }
        }
        return cols;
    }

    private static string? PickColumn(HashSet<string> columns, params string[] candidates)
    {
        foreach (var c in candidates)
        {
            if (columns.Contains(c)) return c;
        }
        return null;
    }

    private static Guid TryParseGuidOrEmpty(string text)
    {
        return Guid.TryParse(text, out var id) ? id : Guid.Empty;
    }

    private static DateTime ParseTimestampOrMin(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return DateTime.MinValue;
        if (DateTime.TryParse(text, out var dt)) return dt;

        if (long.TryParse(text, out var n))
        {
            try
            {
                if (n > 1000000000000L)
                {
                    return DateTimeOffset.FromUnixTimeMilliseconds(n).UtcDateTime;
                }
                if (n > 1000000000L)
                {
                    return DateTimeOffset.FromUnixTimeSeconds(n).UtcDateTime;
                }
                if (n > 0)
                {
                    return new DateTime(n, DateTimeKind.Utc);
                }
            }
            catch
            {
            }
        }

        return DateTime.MinValue;
    }
}
