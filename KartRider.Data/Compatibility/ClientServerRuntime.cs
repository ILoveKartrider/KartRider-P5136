using System;

namespace KartRider.Compatibility;

/// <summary>
/// Selects the server implementation that matches the detected client build.
/// </summary>
public static class ClientServerRuntime
{
    public static bool IsRunning =>
        Korean20051214ServerHost.IsRunning || IsRouterRunning();

    public static void Start(string gameDirectory)
    {
        if (IsRunning)
        {
            return;
        }

        try
        {
            if (ClientBuildProfiles.Active.Build == ClientBuild.Korean20051214)
            {
                Korean20051214ServerHost.Start(gameDirectory);
                return;
            }

            RouterListener.Start();
        }
        catch
        {
            Stop();
            throw;
        }
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
            Console.WriteLine($"Unable to stop the current server: {ex.Message}");
        }
    }

    private static bool IsRouterRunning()
    {
        try
        {
            return RouterListener.Listener?.Server?.IsBound == true;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }
}
