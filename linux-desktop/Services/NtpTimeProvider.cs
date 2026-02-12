using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TwoFactorAuth.Services;

public interface INtpTimeProvider
{
    Task<DateTime?> GetNetworkTimeAsync(string ntpServer, int timeoutMs = 3000);
    Task<TimeSpan?> GetTimeOffsetAsync(string ntpServer, int timeoutMs = 3000);
}

public sealed class NtpTimeProvider : INtpTimeProvider
{
    private const int NtpPort = 123;
    private const int DefaultTimeoutMs = 3000;

    public async Task<DateTime?> GetNetworkTimeAsync(string ntpServer, int timeoutMs = DefaultTimeoutMs)
    {
        try
        {
            var ntpData = new byte[48];
            ntpData[0] = 0x1B; // LI = 0 (no warning), VN = 3 (IPv4 only), Mode = 3 (Client Mode)

            var addresses = await Dns.GetHostAddressesAsync(ntpServer);
            var ipEndPoint = new IPEndPoint(addresses[0], NtpPort);

            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.ReceiveTimeout = timeoutMs;
            socket.SendTimeout = timeoutMs;

            await socket.ConnectAsync(ipEndPoint);
            await socket.SendAsync(ntpData, SocketFlags.None);
            await socket.ReceiveAsync(ntpData, SocketFlags.None);

            const byte serverReplyTime = 40;
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime);
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4);

            intPart = SwapEndianness(intPart);
            fractPart = SwapEndianness(fractPart);

            var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
            var networkDateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)milliseconds);

            return networkDateTime;
        }
        catch
        {
            return null;
        }
    }

    public async Task<TimeSpan?> GetTimeOffsetAsync(string ntpServer, int timeoutMs = DefaultTimeoutMs)
    {
        var networkTime = await GetNetworkTimeAsync(ntpServer, timeoutMs);
        if (networkTime == null)
            return null;

        var localTime = DateTime.UtcNow;
        return networkTime.Value - localTime;
    }

    private static uint SwapEndianness(ulong x)
    {
        return (uint)(((x & 0x000000ff) << 24) +
                      ((x & 0x0000ff00) << 8) +
                      ((x & 0x00ff0000) >> 8) +
                      ((x & 0xff000000) >> 24));
    }
}
