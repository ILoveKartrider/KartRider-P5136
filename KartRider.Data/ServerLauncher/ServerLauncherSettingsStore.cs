using System;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net;
using System.Net.Sockets;
using KartRider.Compatibility;
using Profile;

namespace KartRider.ServerLauncher
{
    internal static class ServerLauncherSettingsStore
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow
        };

        // Do not use "data" here: the P5136 game already owns a case-insensitive
        // Data directory. Server launcher state must stay outside the game assets.
        public static string SettingsPath => Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "ServerData", "server-launcher.json"));

        public static ServerLauncherSettings LoadOrDefault()
        {
            if (!File.Exists(SettingsPath))
            {
                ServerLauncherSettings migrated = ServerLauncherSettings.CreateDefault();
                if (IPAddress.TryParse(ProfileService.SettingConfig.ServerIP, out IPAddress legacyAddress) &&
                    legacyAddress.AddressFamily == AddressFamily.InterNetwork &&
                    !legacyAddress.Equals(IPAddress.Any))
                {
                    // The old field was never used for binding. Preserve it only
                    // as the advertised address while retaining the old Any bind.
                    migrated.BindAddress = IPAddress.IsLoopback(legacyAddress)
                        ? IPAddress.Loopback.ToString()
                        : IPAddress.Any.ToString();
                    migrated.AdvertisedAddress = legacyAddress.ToString();
                }

                try
                {
                    ClientBuildProfiles.Active.Ports.ResolveMessengerPort(
                        ProfileService.SettingConfig.ServerPort);
                    if (ProfileService.SettingConfig.ServerPort != 0)
                    {
                        migrated.ConfiguredPort = ProfileService.SettingConfig.ServerPort;
                    }
                }
                catch (InvalidOperationException)
                {
                }

                return migrated;
            }

            try
            {
                byte[] json = File.ReadAllBytes(SettingsPath);
                return JsonSerializer.Deserialize<ServerLauncherSettings>(json, SerializerOptions)
                    ?? throw new InvalidDataException("서버 설정 JSON 루트가 null입니다.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"서버 설정 JSON이 올바르지 않습니다: {SettingsPath}",
                    exception);
            }
        }

        public static void Save(ServerLauncherSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            settings.Validate(ClientBuildProfiles.Active);

            string path = SettingsPath;
            string directory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("서버 설정 폴더를 확인할 수 없습니다.");
            Directory.CreateDirectory(directory);

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(settings, SerializerOptions);
            string temporaryPath = Path.Combine(
                directory,
                $".{Path.GetFileName(path)}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (FileStream stream = new FileStream(
                           temporaryPath,
                           FileMode.CreateNew,
                           FileAccess.Write,
                           FileShare.None,
                           16 * 1024,
                           FileOptions.WriteThrough))
                {
                    stream.Write(json, 0, json.Length);
                    stream.WriteByte((byte)'\n');
                    stream.Flush(true);
                }

                File.Move(temporaryPath, path, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
    }
}
