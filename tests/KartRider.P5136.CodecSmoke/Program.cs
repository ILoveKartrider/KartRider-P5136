using KartLibrary.Data;
using KartRider.Common.Data;
using KartRider;
using KartRider.Compatibility;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

byte[] source = Encoding.UTF8.GetBytes(string.Concat(
    Enumerable.Repeat("P5136 framework zlib round-trip payload. ", 256)));

foreach ((bool encrypted, bool compressed) in new[]
{
    (false, false),
    (false, true),
    (true, false),
    (true, true)
})
{
    byte[] encoded = DataProcessor.EncodeKRData(
        source,
        encrypted,
        compressed,
        0x51365136u);
    byte[] decoded = DataProcessor.DecodeKRData(encoded);
    if (!source.SequenceEqual(decoded))
    {
        Console.Error.WriteLine(
            $"KRData round trip failed: encrypted={encrypted}, compressed={compressed}");
        return 1;
    }
}

Console.WriteLine("KRData codec smoke test passed (4 modes).");

ClientPortTopology topology = ClientBuildProfiles.Korean5136.Ports;
ushort defaultLoginPort = topology.ResolveLoginTcpPort(topology.DefaultConfiguredPort);
if (topology.ResolveConfiguredPortFromLoginTcp(defaultLoginPort) != topology.DefaultConfiguredPort)
{
    Console.Error.WriteLine("P5136 login/configured port conversion failed.");
    return 1;
}
if (topology.MaximumLoginTcpPort != 65534)
{
    Console.Error.WriteLine("P5136 maximum login TCP port calculation failed.");
    return 1;
}
try
{
    topology.ResolveConfiguredPortFromLoginTcp(1);
    Console.Error.WriteLine("P5136 invalid login TCP lower bound was accepted.");
    return 1;
}
catch (InvalidOperationException)
{
}

ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean5136);
ushort testBasePort = FindAvailablePortBlock();
P5136ServerOptions serverOptions = new P5136ServerOptions
{
    BindAddress = IPAddress.Loopback,
    AdvertisedAddress = IPAddress.Loopback,
    ConfiguredPort = testBasePort,
    LogDirectory = Path.Combine(Path.GetTempPath(), "KartRider-P5136-smoke-logs"),
    EnablePacketTrace = false
};

ClientServerRuntime.Start(AppContext.BaseDirectory, serverOptions);
try
{
    if (!ClientServerRuntime.IsRunning ||
        RouterListener.Listener?.LocalEndpoint is not IPEndPoint loginEndPoint ||
        !loginEndPoint.Address.Equals(IPAddress.Loopback) ||
        loginEndPoint.Port != topology.ResolveLoginTcpPort(testBasePort) ||
        RouterListener.UDPServer?.LocalEndPoint?.Port != topology.ResolveUdpPort(testBasePort) ||
        RouterListener.P2PServer?.LocalEndPoint?.Port != topology.ResolveP2pPort(testBasePort) ||
        RouterListener.MsgrServer?.LocalEndPoint?.Port != topology.ResolveMessengerPort(testBasePort))
    {
        Console.Error.WriteLine("P5136 listener bind smoke test failed.");
        return 1;
    }

    RouterListener.UDPServer.Stop();
    if (ClientServerRuntime.IsRunning || !ClientServerRuntime.HasResources)
    {
        Console.Error.WriteLine("P5136 partial-listener state detection failed.");
        return 1;
    }
}
finally
{
    ClientServerRuntime.Stop();
}

if (ClientServerRuntime.HasResources)
{
    Console.Error.WriteLine("P5136 partial-listener cleanup failed.");
    return 1;
}

ushort conflictBasePort = FindAvailablePortBlock();
using (UdpClient conflict = new UdpClient(new IPEndPoint(
           IPAddress.Loopback,
           topology.ResolveUdpPort(conflictBasePort))))
{
    serverOptions.ConfiguredPort = conflictBasePort;
    bool failedAsExpected = false;
    try
    {
        ClientServerRuntime.Start(AppContext.BaseDirectory, serverOptions);
    }
    catch (SocketException)
    {
        failedAsExpected = true;
    }

    if (!failedAsExpected || ClientServerRuntime.IsRunning)
    {
        ClientServerRuntime.Stop();
        Console.Error.WriteLine("P5136 listener rollback smoke test failed.");
        return 1;
    }
}

Console.WriteLine("P5136 bind/advertise/port lifecycle smoke test passed.");

ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean20051214);
ushort p236BasePort = FindAvailablePortBlock();
serverOptions.BindAddress = IPAddress.Loopback;
serverOptions.AdvertisedAddress = IPAddress.Loopback;
serverOptions.ConfiguredPort = p236BasePort;
ClientServerRuntime.Start(AppContext.BaseDirectory, serverOptions);
try
{
    int expectedPort = ClientBuildProfiles.Korean20051214.Ports.ResolveLoginTcpPort(p236BasePort);
    IPEndPoint p236Tcp = IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveTcpListeners()
        .FirstOrDefault(endpoint => endpoint.Port == expectedPort);
    IPEndPoint p236Udp = IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveUdpListeners()
        .FirstOrDefault(endpoint => endpoint.Port == expectedPort);
    if (p236Tcp == null || p236Udp == null ||
        !p236Tcp.Address.Equals(IPAddress.Loopback) ||
        !p236Udp.Address.Equals(IPAddress.Loopback))
    {
        Console.Error.WriteLine("P236 bind-address compatibility smoke test failed.");
        return 1;
    }
}
finally
{
    ClientServerRuntime.Stop();
    ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean5136);
}

Console.WriteLine("P236 explicit bind-address smoke test passed.");

if (args.Length == 1)
{
    PINFile pin = new PINFile(args[0]);
    if (pin.Header.MinorVersion != 5136)
    {
        Console.Error.WriteLine($"Unexpected PIN protocol: {pin.Header.MinorVersion}");
        return 1;
    }
    Console.WriteLine("P5136 PIN read smoke test passed.");
}

return 0;

static ushort FindAvailablePortBlock()
{
    for (int candidate = 43000; candidate <= 62000; candidate += 3)
    {
        TcpListener login = null;
        TcpListener messenger = null;
        UdpClient game = null;
        UdpClient p2p = null;
        try
        {
            login = new TcpListener(IPAddress.Loopback, candidate + 1);
            messenger = new TcpListener(IPAddress.Loopback, candidate + 2);
            game = new UdpClient(new IPEndPoint(IPAddress.Loopback, candidate));
            p2p = new UdpClient(new IPEndPoint(IPAddress.Loopback, candidate + 1));
            login.Start();
            messenger.Start();
            return checked((ushort)candidate);
        }
        catch (SocketException)
        {
        }
        finally
        {
            login?.Stop();
            messenger?.Stop();
            game?.Dispose();
            p2p?.Dispose();
        }
    }

    throw new InvalidOperationException("Could not find an available P5136 port block for the smoke test.");
}
