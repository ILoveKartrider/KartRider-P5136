using System;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace KartRider.Compatibility
{
    /// <summary>
    /// Network and diagnostic settings owned by the P5136 server process.
    /// These settings intentionally do not use Profile/Settings.json, which is
    /// shared with the connector when both executables are beside the client.
    /// </summary>
    public sealed class P5136ServerOptions
    {
        public IPAddress BindAddress { get; set; } = IPAddress.Any;

        public IPAddress AdvertisedAddress { get; set; } = IPAddress.Loopback;

        public ushort ConfiguredPort { get; set; } =
            ClientBuildProfiles.Korean5136.Ports.DefaultConfiguredPort;

        public string LogDirectory { get; set; } =
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs"));

        public bool EnablePacketTrace { get; set; } = true;

        public void Validate(ClientBuildProfile profile = null)
        {
            if (BindAddress == null || BindAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("The bind address must be an IPv4 address.", nameof(BindAddress));
            }

            if (AdvertisedAddress == null ||
                AdvertisedAddress.AddressFamily != AddressFamily.InterNetwork ||
                IPAddress.Any.Equals(AdvertisedAddress))
            {
                throw new ArgumentException(
                    "The advertised address must be a concrete IPv4 address (not 0.0.0.0).",
                    nameof(AdvertisedAddress));
            }

            if (string.IsNullOrWhiteSpace(LogDirectory))
            {
                throw new ArgumentException("A log directory is required.", nameof(LogDirectory));
            }

            _ = Path.GetFullPath(LogDirectory);

            ClientPortTopology ports = (profile ?? ClientBuildProfiles.Korean5136).Ports;
            _ = ports.ResolveLoginTcpPort(ConfiguredPort);
            _ = ports.ResolveUdpPort(ConfiguredPort);
            _ = ports.ResolveP2pPort(ConfiguredPort);
            _ = ports.ResolveMessengerPort(ConfiguredPort);
        }

        public P5136ServerOptions Clone()
        {
            return new P5136ServerOptions
            {
                BindAddress = BindAddress,
                AdvertisedAddress = AdvertisedAddress,
                ConfiguredPort = ConfiguredPort,
                LogDirectory = Path.GetFullPath(LogDirectory),
                EnablePacketTrace = EnablePacketTrace
            };
        }
    }
}
