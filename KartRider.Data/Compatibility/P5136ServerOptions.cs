using System;
using System.Collections.Generic;
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

        public int FirstMessageDelayMilliseconds { get; set; } = 250;

        public ItemProbabilityRankBand ItemProbabilityRankBand { get; set; } =
            ItemProbabilityRankBand.Live;

        public List<ItemProbabilityEntry> IndividualItemProbabilities { get; set; } =
            new List<ItemProbabilityEntry>();

        public List<ItemProbabilityEntry> TeamItemProbabilities { get; set; } =
            new List<ItemProbabilityEntry>();

        public RandomTrackConfiguration RandomTracks { get; set; } =
            new RandomTrackConfiguration();

        public void Validate(ClientBuildProfile profile = null)
        {
            if (BindAddress == null || BindAddress.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("수신 주소는 IPv4 주소여야 합니다.", nameof(BindAddress));
            }

            if (AdvertisedAddress == null ||
                AdvertisedAddress.AddressFamily != AddressFamily.InterNetwork ||
                IPAddress.Any.Equals(AdvertisedAddress))
            {
                throw new ArgumentException(
                    "광고 주소에는 0.0.0.0이 아닌 실제 IPv4 주소를 입력해야 합니다.",
                    nameof(AdvertisedAddress));
            }

            if (string.IsNullOrWhiteSpace(LogDirectory))
            {
                throw new ArgumentException("로그 폴더를 지정해야 합니다.", nameof(LogDirectory));
            }

            if (FirstMessageDelayMilliseconds < 0 || FirstMessageDelayMilliseconds > 5000)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(FirstMessageDelayMilliseconds),
                    "초기 핸드셰이크 지연은 0~5000ms 범위여야 합니다.");
            }

            ItemProbabilityService.Validate(
                new ItemProbabilityConfiguration
                {
                    RankBand = ItemProbabilityRankBand,
                    Individual = ItemProbabilityConfiguration.CloneEntries(
                        IndividualItemProbabilities),
                    Team = ItemProbabilityConfiguration.CloneEntries(
                        TeamItemProbabilities)
                },
                allowEmptyTables: true);
            (RandomTracks ?? new RandomTrackConfiguration()).Validate();

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
                EnablePacketTrace = EnablePacketTrace,
                FirstMessageDelayMilliseconds = FirstMessageDelayMilliseconds,
                ItemProbabilityRankBand = ItemProbabilityRankBand,
                IndividualItemProbabilities = ItemProbabilityConfiguration.CloneEntries(
                    IndividualItemProbabilities),
                TeamItemProbabilities = ItemProbabilityConfiguration.CloneEntries(
                    TeamItemProbabilities),
                RandomTracks = (RandomTracks ?? new RandomTrackConfiguration()).Clone()
            };
        }
    }
}
