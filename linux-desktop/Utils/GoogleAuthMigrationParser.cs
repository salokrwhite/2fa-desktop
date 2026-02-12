using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using TwoFactorAuth.Models;

namespace TwoFactorAuth.Utils;

public static class GoogleAuthMigrationParser
{
    public static bool TryParse(string uri, out List<Account> accounts, out string error)
    {
        accounts = new List<Account>();
        error = string.Empty;

        try
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsedUri))
            {
                error = "Invalid URI format";
                return false;
            }

            if (!parsedUri.Scheme.Equals("otpauth-migration", StringComparison.OrdinalIgnoreCase))
            {
                error = "Not a Google Authenticator migration URI";
                return false;
            }
            var query = parsedUri.Query;
            var dataParam = HttpUtility.ParseQueryString(query)["data"];
            
            if (string.IsNullOrEmpty(dataParam))
            {
                error = "Missing data parameter";
                return false;
            }

            byte[] protoData;
            try
            {
                protoData = Convert.FromBase64String(dataParam);
            }
            catch (FormatException)
            {
                error = "Invalid Base64 data";
                return false;
            }

            var payload = ParseMigrationPayload(protoData);
            
            if (payload.OtpParameters == null || payload.OtpParameters.Count == 0)
            {
                error = "No OTP parameters found in migration data";
                return false;
            }

            foreach (var otp in payload.OtpParameters)
            {
                var account = ConvertToAccount(otp);
                if (account != null)
                {
                    accounts.Add(account);
                }
            }

            if (accounts.Count == 0)
            {
                error = "Failed to parse any valid accounts";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = $"Parse error: {ex.Message}";
            return false;
        }
    }

    private static MigrationPayload ParseMigrationPayload(byte[] data)
    {
        var payload = new MigrationPayload();
        using var stream = new MemoryStream(data);
        
        while (stream.Position < stream.Length)
        {
            var tag = ReadVarint(stream);
            var fieldNumber = (int)(tag >> 3);
            var wireType = (int)(tag & 0x7);

            switch (fieldNumber)
            {
                case 1: 
                    if (wireType == 2) 
                    {
                        var length = (int)ReadVarint(stream);
                        var otpData = new byte[length];
                        stream.Read(otpData, 0, length);
                        payload.OtpParameters.Add(ParseOtpParameters(otpData));
                    }
                    break;
                case 2: 
                    if (wireType == 0) payload.Version = (int)ReadVarint(stream);
                    break;
                case 3: 
                    if (wireType == 0) payload.BatchSize = (int)ReadVarint(stream);
                    break;
                case 4: 
                    if (wireType == 0) payload.BatchIndex = (int)ReadVarint(stream);
                    break;
                case 5: 
                    if (wireType == 0) payload.BatchId = (int)ReadVarint(stream);
                    break;
                default:
                    SkipField(stream, wireType);
                    break;
            }
        }

        return payload;
    }

    private static OtpParameters ParseOtpParameters(byte[] data)
    {
        var otp = new OtpParameters();
        using var stream = new MemoryStream(data);

        while (stream.Position < stream.Length)
        {
            var tag = ReadVarint(stream);
            var fieldNumber = (int)(tag >> 3);
            var wireType = (int)(tag & 0x7);

            switch (fieldNumber)
            {
                case 1: 
                    if (wireType == 2)
                    {
                        var length = (int)ReadVarint(stream);
                        otp.Secret = new byte[length];
                        stream.Read(otp.Secret, 0, length);
                    }
                    break;
                case 2: 
                    if (wireType == 2)
                    {
                        var length = (int)ReadVarint(stream);
                        var bytes = new byte[length];
                        stream.Read(bytes, 0, length);
                        otp.Name = Encoding.UTF8.GetString(bytes);
                    }
                    break;
                case 3: 
                    if (wireType == 2)
                    {
                        var length = (int)ReadVarint(stream);
                        var bytes = new byte[length];
                        stream.Read(bytes, 0, length);
                        otp.Issuer = Encoding.UTF8.GetString(bytes);
                    }
                    break;
                case 4: 
                    if (wireType == 0) otp.Algorithm = (Algorithm)ReadVarint(stream);
                    break;
                case 5: 
                    if (wireType == 0) otp.Digits = (DigitCount)ReadVarint(stream);
                    break;
                case 6: 
                    if (wireType == 0) otp.Type = (OtpTypeEnum)ReadVarint(stream);
                    break;
                case 7: 
                    if (wireType == 0) otp.Counter = (long)ReadVarint(stream);
                    break;
                default:
                    SkipField(stream, wireType);
                    break;
            }
        }

        return otp;
    }

    private static ulong ReadVarint(Stream stream)
    {
        ulong result = 0;
        int shift = 0;

        while (true)
        {
            var b = stream.ReadByte();
            if (b == -1) throw new EndOfStreamException();

            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) break;
            shift += 7;
        }

        return result;
    }

    private static void SkipField(Stream stream, int wireType)
    {
        switch (wireType)
        {
            case 0: 
                ReadVarint(stream);
                break;
            case 1: 
                stream.Seek(8, SeekOrigin.Current);
                break;
            case 2: 
                var length = (int)ReadVarint(stream);
                stream.Seek(length, SeekOrigin.Current);
                break;
            case 5: 
                stream.Seek(4, SeekOrigin.Current);
                break;
            default:
                throw new InvalidDataException($"Unknown wire type: {wireType}");
        }
    }

    private static Account? ConvertToAccount(OtpParameters otp)
    {
        if (otp.Secret == null || otp.Secret.Length == 0)
            return null;

        var secretBase32 = Base32.Encode(otp.Secret);

        string name = otp.Name ?? string.Empty;
        string issuer = otp.Issuer ?? string.Empty;
        if (string.IsNullOrEmpty(issuer) && name.Contains(':'))
        {
            var parts = name.Split(':', 2);
            issuer = parts[0].Trim();
            name = parts[1].Trim();
        }

        if (string.IsNullOrEmpty(name))
        {
            name = issuer;
        }

        int digits = otp.Digits switch
        {
            DigitCount.SIX => 6,
            DigitCount.EIGHT => 8,
            _ => 6
        };

        var otpType = otp.Type switch
        {
            OtpTypeEnum.HOTP => OtpType.Hotp,
            OtpTypeEnum.TOTP => OtpType.Totp,
            _ => OtpType.Totp
        };

        return new Account
        {
            Name = name,
            Issuer = issuer,
            Secret = secretBase32,
            Type = otpType,
            Digits = digits,
            Period = 30, 
            Counter = (int)otp.Counter
        };
    }

    private class MigrationPayload
    {
        public List<OtpParameters> OtpParameters { get; } = new();
        public int Version { get; set; }
        public int BatchSize { get; set; }
        public int BatchIndex { get; set; }
        public int BatchId { get; set; }
    }

    private class OtpParameters
    {
        public byte[]? Secret { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public Algorithm Algorithm { get; set; }
        public DigitCount Digits { get; set; }
        public OtpTypeEnum Type { get; set; }
        public long Counter { get; set; }
    }

    private enum Algorithm
    {
        UNSPECIFIED = 0,
        SHA1 = 1,
        SHA256 = 2,
        SHA512 = 3,
        MD5 = 4
    }

    private enum DigitCount
    {
        UNSPECIFIED = 0,
        SIX = 1,
        EIGHT = 2
    }

    private enum OtpTypeEnum
    {
        UNSPECIFIED = 0,
        HOTP = 1,
        TOTP = 2
    }
}
