using System.Text.Json.Serialization;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Services;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.Never)]
[JsonSerializable(typeof(BackupFile))]
[JsonSerializable(typeof(BackupData))]
[JsonSerializable(typeof(BackupMetadata))]
[JsonSerializable(typeof(BackupAccount))]
[JsonSerializable(typeof(BackupCategory))]
[JsonSerializable(typeof(BackupOperationLog))]
internal partial class BackupJsonContext : JsonSerializerContext
{
}
