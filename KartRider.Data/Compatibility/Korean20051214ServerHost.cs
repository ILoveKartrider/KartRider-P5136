extern alias Korean20051214Legacy;

using LegacyOptions = Korean20051214Legacy::KartRider.Legacy.Korean20051214.Korean20051214Options;
using LegacyServer = Korean20051214Legacy::KartRider.Legacy.Korean20051214.Korean20051214Server;
using Profile;
using System;
using System.IO;

namespace KartRider.Compatibility;

/// <summary>
/// Adapts the current JSON profile to the isolated 2005-12-14 server module.
/// </summary>
public static class Korean20051214ServerHost
{
    public static bool IsRunning => LegacyServer.IsRunning;

    public static void Start(string gameDirectory, P5136ServerOptions serverOptions)
    {
        ClientBuildProfile build = ClientBuildProfiles.Active;
        if (build.Build != ClientBuild.Korean20051214)
            throw new InvalidOperationException("The Korean 2005 server requires the Korean20051214 client profile.");

        string username = ProfileService.SettingConfig.Name;
        if (string.IsNullOrWhiteSpace(username))
            username = "WIZONE";

        ProfileConfig config = ProfileService.GetProfileConfig(username);
        Profile.RiderData rider = config.Rider;
        Profile.RiderItemData item = config.RiderItem;

        LegacyOptions options = new LegacyOptions
        {
            BindIP = serverOptions.BindAddress.ToString(),
            ServerIP = serverOptions.AdvertisedAddress.ToString(),
            TcpPort = build.Ports.ResolveLoginTcpPort(serverOptions.ConfiguredPort),
            UdpPort = build.Ports.ResolveUdpPort(serverOptions.ConfiguredPort),
            Username = username,
            Nickname = username,
            Lucci = rider.Lucci,
            Rp = rider.RP > int.MaxValue ? int.MaxValue : (int)rider.RP,
            SlotChanger = rider.SlotChanger > short.MaxValue ? short.MaxValue : (short)rider.SlotChanger,
            PreventItem = config.ServerSetting.PreventItem_Use != 0,
            Character = unchecked((short)item.Set_Character),
            Paint = unchecked((short)item.Set_Paint),
            Kart = unchecked((short)item.Set_Kart),
            Plate = unchecked((short)item.Set_Plate),
            Goggle = unchecked((short)item.Set_Goggle),
            Balloon = unchecked((short)item.Set_Balloon),
            HeadBand = unchecked((short)item.Set_HeadBand),
            ProfileRootDirectory = Path.Combine(Path.GetFullPath(gameDirectory), "Profile", "Launcher"),
            LoadProfileFiles = false
        };

        LegacyServer.Start(options);
        Console.WriteLine(
            $"Started Korean 2005-12-14 server on {options.ServerIP}:" +
            $"{options.TcpPort}/TCP and {options.UdpPort}/UDP.");
    }

    public static void Stop()
    {
        LegacyServer.Stop();
    }
}
