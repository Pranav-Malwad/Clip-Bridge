using System.Net;
using System.Net.Sockets;

namespace ClipBridge.Windows;

public static class NetworkAddress
{
    public static string FindLanIpv4() => Dns.GetHostEntry(Dns.GetHostName()).AddressList
        .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
        ?.ToString() ?? "127.0.0.1";
}
