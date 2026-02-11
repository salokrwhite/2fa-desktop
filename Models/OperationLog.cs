using System;

namespace TwoFactorAuth.Models;

public class OperationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Operation { get; set; } = string.Empty; 
    public string Target { get; set; } = string.Empty; 
    public string Details { get; set; } = string.Empty;
}
