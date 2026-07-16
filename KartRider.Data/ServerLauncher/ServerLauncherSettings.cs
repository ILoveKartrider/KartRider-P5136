using KartRider.Compatibility;
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace KartRider.ServerLauncher
{
    internal sealed class ServerLauncherSettings
    {
        public const int CurrentVersion = 1;

        public int Version { get; set; } = CurrentVersion;

        public string BindAddress { get; set; } = IPAddress.Loopback.ToString();

        public string AdvertisedAddress { get; set; } = IPAddress.Loopback.ToString();

        public int ConfiguredPort { get; set; } =
            ClientBuildProfiles.Korean5136.Ports.DefaultConfiguredPort;

        public string LogDirectory { get; set; } = DefaultLogDirectory;

        public bool EnablePacketTrace { get; set; } = true;

        public static string DefaultLogDirectory =>
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "logs"));

        public static ServerLauncherSettings CreateDefault()
        {
            return new ServerLauncherSettings();
        }

        public P5136ServerOptions ToServerOptions(ClientBuildProfile profile)
        {
            Validate(profile);
            return new P5136ServerOptions
            {
                BindAddress = IPAddress.Parse(BindAddress),
                AdvertisedAddress = IPAddress.Parse(AdvertisedAddress),
                ConfiguredPort = checked((ushort)ConfiguredPort),
                LogDirectory = Path.GetFullPath(LogDirectory),
                EnablePacketTrace = EnablePacketTrace
            };
        }

        public void Validate(ClientBuildProfile profile)
        {
            if (Version != CurrentVersion)
            {
                throw new InvalidDataException(
                    $"지원하지 않는 서버 설정 버전입니다: {Version} (expected {CurrentVersion}).");
            }

            ValidateIpv4(BindAddress, "바인드 IPv4", allowAny: true);
            ValidateIpv4(AdvertisedAddress, "광고 IPv4", allowAny: false);
            if (ConfiguredPort < 1 || ConfiguredPort > IPEndPoint.MaxPort)
            {
                throw new InvalidDataException("기준 포트는 1~65535 범위여야 합니다.");
            }

            if (string.IsNullOrWhiteSpace(LogDirectory) ||
                LogDirectory.Length > 1024 ||
                LogDirectory.Any(char.IsControl))
            {
                throw new InvalidDataException("로그 폴더 경로가 올바르지 않습니다.");
            }

            P5136ServerOptions options = new P5136ServerOptions
            {
                BindAddress = IPAddress.Parse(BindAddress),
                AdvertisedAddress = IPAddress.Parse(AdvertisedAddress),
                ConfiguredPort = checked((ushort)ConfiguredPort),
                LogDirectory = Path.GetFullPath(LogDirectory),
                EnablePacketTrace = EnablePacketTrace
            };
            options.Validate(profile);
        }

        public static IPAddress FindPreferredLanAddress()
        {
            try
            {
                var candidates = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(adapter =>
                        adapter.OperationalStatus == OperationalStatus.Up &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        adapter.NetworkInterfaceType != NetworkInterfaceType.Tunnel &&
                        !LooksVirtual(adapter))
                    .SelectMany(adapter =>
                    {
                        IPInterfaceProperties properties = adapter.GetIPProperties();
                        bool hasGateway = properties.GatewayAddresses.Any(item =>
                            item.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !item.Address.Equals(IPAddress.Any));
                        return properties.UnicastAddresses.Select(item => new
                        {
                            item.Address,
                            HasGateway = hasGateway,
                            IsPhysical = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 ||
                                         adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                                         adapter.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet ||
                                         adapter.NetworkInterfaceType == NetworkInterfaceType.FastEthernetFx ||
                                         adapter.NetworkInterfaceType == NetworkInterfaceType.FastEthernetT
                        });
                    })
                    .Where(item =>
                        item.Address.AddressFamily == AddressFamily.InterNetwork &&
                        !IPAddress.IsLoopback(item.Address) &&
                        !item.Address.Equals(IPAddress.Any) &&
                        !item.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
                    .OrderByDescending(item => item.HasGateway)
                    .ThenByDescending(item => item.IsPhysical)
                    .ThenByDescending(item => IsPrivateAddress(item.Address))
                    .ThenBy(item => item.Address.ToString(), StringComparer.Ordinal)
                    .Select(item => item.Address)
                    .ToArray();

                if (candidates.Length > 0)
                {
                    return candidates[0];
                }
            }
            catch (NetworkInformationException)
            {
            }

            return IPAddress.Loopback;
        }

        private static void ValidateIpv4(string value, string label, bool allowAny)
        {
            if (!IPAddress.TryParse(value, out IPAddress address) ||
                address.AddressFamily != AddressFamily.InterNetwork ||
                (!allowAny && IPAddress.Any.Equals(address)))
            {
                string suffix = allowAny ? string.Empty : " (0.0.0.0 제외)";
                throw new InvalidDataException($"{label} 값은 올바른 IPv4 주소여야 합니다{suffix}.");
            }
        }

        private static bool IsPrivateAddress(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return bytes[0] == 10 ||
                   bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31 ||
                   bytes[0] == 192 && bytes[1] == 168;
        }

        private static bool LooksVirtual(NetworkInterface adapter)
        {
            string identity = (adapter.Name + " " + adapter.Description).ToLowerInvariant();
            return identity.Contains("virtual") ||
                   identity.Contains("vethernet") ||
                   identity.Contains("hyper-v") ||
                   identity.Contains("vmware") ||
                   identity.Contains("virtualbox") ||
                   identity.Contains("tailscale") ||
                   identity.Contains("wsl");
        }
    }
}
