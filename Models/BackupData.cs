using System;
using System.Collections.Generic;

namespace TwoFactorAuth.Models;

public sealed class BackupFile
{
    public string Version { get; set; } = "1.0";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string AppVersion { get; set; } = "1.0.0";
    public BackupMetadata Metadata { get; set; } = new();
    public string EncryptedData { get; set; } = string.Empty;
    public string Signature { get; set; } = string.Empty;
}

public sealed class BackupMetadata
{
    public int AccountCount { get; set; }
    public int CategoryCount { get; set; }
    public bool HasSettings { get; set; }
    public bool HasLogs { get; set; }
}

public sealed class BackupData
{
    public List<BackupAccount> Accounts { get; set; } = new();
    public List<BackupCategory> Categories { get; set; } = new();
    public Dictionary<string, string> Settings { get; set; } = new();
    public List<BackupOperationLog> OperationLogs { get; set; } = new();
}

public sealed class BackupAccount
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string Type { get; set; } = "TOTP";
    public int Digits { get; set; } = 6;
    public int Period { get; set; } = 30;
    public int Counter { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? GroupName { get; set; }
    public string? Icon { get; set; }
    public bool IsFavorite { get; set; }
}

public sealed class BackupCategory
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

public sealed class BackupOperationLog
{
    public Guid Id { get; set; }
    public DateTime Timestamp { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Details { get; set; }
}
