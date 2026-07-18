using System;
using System.Net;

namespace KartRider.Compatibility;

/// <summary>
/// Selects the server implementation that matches the detected client build.
/// </summary>
public static class ClientServerRuntime
{
    private static readonly object SyncRoot = new object();
    private static P5136ServerOptions currentOptions = new P5136ServerOptions();

    public static bool IsRunning =>
        Korean20051214ServerHost.IsRunning || IsRouterRunning();

    public static bool HasResources =>
        Korean20051214ServerHost.IsRunning || RouterListener.HasResources;

    public static P5136ServerOptions CurrentOptions
    {
        get
        {
            lock (SyncRoot)
            {
                return currentOptions.Clone();
            }
        }
    }

    public static IPAddress AdvertisedAddress => CurrentOptions.AdvertisedAddress;

    public static ushort ConfiguredPort => CurrentOptions.ConfiguredPort;

    public static int FirstMessageDelayMilliseconds =>
        CurrentOptions.FirstMessageDelayMilliseconds;

    public static void Start(string gameDirectory, P5136ServerOptions options)
    {
        if (IsRunning)
        {
            return;
        }

        if (HasResources)
        {
            Stop();
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        options.Validate(ClientBuildProfiles.Active);
        P5136ServerOptions snapshot = options.Clone();
        lock (SyncRoot)
        {
            currentOptions = snapshot;
        }

        try
        {
            if (ClientBuildProfiles.Active.Build == ClientBuild.Korean20051214)
            {
                Korean20051214ServerHost.Start(gameDirectory, snapshot);
                return;
            }

            ItemProbabilityService.Configure(
                gameDirectory,
                new ItemProbabilityConfiguration
                {
                    RankBand = snapshot.ItemProbabilityRankBand,
                    Individual = ItemProbabilityConfiguration.CloneEntries(
                        snapshot.IndividualItemProbabilities),
                    Team = ItemProbabilityConfiguration.CloneEntries(
                        snapshot.TeamItemProbabilities)
                });
            Korean5136RandomTrackService.Configure(
                gameDirectory,
                snapshot.RandomTracks);
            RouterListener.Start(snapshot);
        }
        catch
        {
            Stop();
            throw;
        }
    }

    [Obsolete("Pass explicit server options so bind and advertised addresses cannot be confused.")]
    public static void Start(string gameDirectory)
    {
        IPAddress advertisedAddress = IPAddress.TryParse(
            Profile.ProfileService.SettingConfig.ServerIP,
            out IPAddress parsedAddress)
            ? parsedAddress
            : IPAddress.Loopback;

        Start(gameDirectory, new P5136ServerOptions
        {
            BindAddress = IPAddress.Any,
            AdvertisedAddress = advertisedAddress,
            ConfiguredPort = Profile.ProfileService.SettingConfig.ServerPort
        });
    }

    public static void Stop()
    {
        if (Korean20051214ServerHost.IsRunning)
        {
            Korean20051214ServerHost.Stop();
            return;
        }

        try
        {
            RouterListener.Stop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"현재 서버 중지 실패: {ex.Message}");
        }
    }

    private static bool IsRouterRunning()
    {
        try
        {
            return RouterListener.IsRunning;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
