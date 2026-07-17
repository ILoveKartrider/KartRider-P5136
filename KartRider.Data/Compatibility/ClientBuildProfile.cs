using KartRider.Common.Data;
using KartRider.Common.Security;
using KartRider.IO.Packet;
using Profile;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace KartRider.Compatibility
{
    public enum ClientBuild
    {
        Modern,
        Korean5136,
        Korean20051214
    }

    public sealed class ClientPortTopology
    {
        public ushort DefaultConfiguredPort { get; }
        public int LoginTcpOffset { get; }
        public int UdpOffset { get; }
        public int P2pOffset { get; }
        public int MessengerOffset { get; }
        public ushort DefaultLoginTcpPort => AddOffset(DefaultConfiguredPort, LoginTcpOffset);
        public ushort MaximumLoginTcpPort => checked((ushort)(
            IPEndPoint.MaxPort - Math.Max(0, MessengerOffset - LoginTcpOffset)));

        public ClientPortTopology(
            ushort defaultConfiguredPort,
            int loginTcpOffset,
            int udpOffset,
            int p2pOffset,
            int messengerOffset)
        {
            DefaultConfiguredPort = defaultConfiguredPort;
            LoginTcpOffset = loginTcpOffset;
            UdpOffset = udpOffset;
            P2pOffset = p2pOffset;
            MessengerOffset = messengerOffset;
        }

        public ushort ResolveConfiguredPort(ushort configuredPort)
        {
            return configuredPort == 0 ? DefaultConfiguredPort : configuredPort;
        }

        public ushort ResolveLoginTcpPort(ushort configuredPort) => AddOffset(ResolveConfiguredPort(configuredPort), LoginTcpOffset);

        public ushort ResolveUdpPort(ushort configuredPort) => AddOffset(ResolveConfiguredPort(configuredPort), UdpOffset);

        public ushort ResolveP2pPort(ushort configuredPort) => AddOffset(ResolveConfiguredPort(configuredPort), P2pOffset);

        public ushort ResolveMessengerPort(ushort configuredPort) => AddOffset(ResolveConfiguredPort(configuredPort), MessengerOffset);

        public ushort ResolveConfiguredPortFromLoginTcp(ushort loginTcpPort)
        {
            int result = loginTcpPort - LoginTcpOffset;
            if (result < 1 || result > IPEndPoint.MaxPort)
                throw new InvalidOperationException($"올바르지 않은 로그인 TCP 포트: {loginTcpPort}");
            return (ushort)result;
        }

        private static ushort AddOffset(ushort port, int offset)
        {
            int result = port + offset;
            if (result < IPEndPoint.MinPort || result > IPEndPoint.MaxPort)
                throw new InvalidOperationException($"올바르지 않은 클라이언트 포트: {result}");
            return (ushort)result;
        }
    }

    public sealed class ClientBuildProfile
    {
        public ClientBuild Build { get; }
        public string DisplayName { get; }
        public string ExecutableSha256 { get; }
        public ushort ProtocolVersion { get; }
        public ushort? PinVersion { get; }
        public ushort ExecutableVersion { get; }
        public string BuildDate { get; }
        public ushort LocaleId { get; }
        public ushort ClientLocation { get; }
        public string Service { get; }
        public bool UsesModernPin { get; }
        public bool SkipUpdater { get; }
        public bool SkipRhoDump { get; }
        public string ProfileRelativePath { get; }
        public ClientPortTopology Ports { get; }

        public bool IsLegacy => Build != ClientBuild.Modern;

        public ClientBuildProfile(
            ClientBuild build,
            string displayName,
            string executableSha256,
            ushort protocolVersion,
            ushort? pinVersion,
            ushort executableVersion,
            string buildDate,
            ushort localeId,
            ushort clientLocation,
            string service,
            bool usesModernPin,
            bool skipUpdater,
            bool skipRhoDump,
            string profileRelativePath,
            ClientPortTopology ports)
        {
            Build = build;
            DisplayName = displayName;
            ExecutableSha256 = executableSha256;
            ProtocolVersion = protocolVersion;
            PinVersion = pinVersion;
            ExecutableVersion = executableVersion;
            BuildDate = buildDate;
            LocaleId = localeId;
            ClientLocation = clientLocation;
            Service = service;
            UsesModernPin = usesModernPin;
            SkipUpdater = skipUpdater;
            SkipRhoDump = skipRhoDump;
            ProfileRelativePath = profileRelativePath;
            Ports = ports ?? throw new ArgumentNullException(nameof(ports));
        }
    }

    public static class ClientBuildProfiles
    {
        public static ClientBuildProfile Modern { get; } = new ClientBuildProfile(
            ClientBuild.Modern,
            "현대 PIN 클라이언트",
            string.Empty,
            0,
            null,
            0,
            string.Empty,
            0,
            0,
            string.Empty,
            true,
            false,
            false,
            string.Empty,
            new ClientPortTopology(39311, 0, 0, 1, 2));

        public static ClientBuildProfile Korean5136 { get; } = new ClientBuildProfile(
            ClientBuild.Korean5136,
            "한국 5136",
            "629F084E2A12C6FA1FF0EA603B90F8768454D13A1BC2DF6A8504F8AA06FD6194",
            5136,
            5136,
            3617,
            string.Empty,
            1002,
            118,
            "kr",
            false,
            true,
            true,
            Path.Combine("Profile", "kr", "launcher.xml"),
            new ClientPortTopology(39311, 1, 0, 1, 2));

        public static ClientBuildProfile Korean20051214 { get; } = new ClientBuildProfile(
            ClientBuild.Korean20051214,
            "한국 2005-12-14",
            "81C6E1CD14102D3937DB9933FCF83908049132D6F0ACA9F6CA153C1D9D23797A",
            236,
            236,
            223,
            "20051214",
            1002,
            118,
            "kr",
            false,
            true,
            true,
            Path.Combine("Profile", "launcher.xml"),
            new ClientPortTopology(39311, 1, 1, 1, 2));

        public static ClientBuildProfile Active { get; private set; } = Modern;

        public static void SetActive(ClientBuildProfile profile)
        {
            Active = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        public static ClientBuildProfile For(ClientBuild build)
        {
            return build switch
            {
                ClientBuild.Korean5136 => Korean5136,
                ClientBuild.Korean20051214 => Korean20051214,
                _ => Modern
            };
        }
    }

    public readonly struct ShallowPinHeader
    {
        public ushort LocaleId { get; }
        public ushort ClientLocation { get; }
        public ushort MinorVersion { get; }

        public ShallowPinHeader(ushort localeId, ushort clientLocation, ushort minorVersion)
        {
            LocaleId = localeId;
            ClientLocation = clientLocation;
            MinorVersion = minorVersion;
        }
    }

    public static class ClientBuildDetector
    {
        private const uint PinMagic = 0x10EF037E;

        public static ClientBuildProfile DetectAndActivate(string gameDirectory)
        {
            ClientBuildProfile profile = Detect(gameDirectory);
            ClientBuildProfiles.SetActive(profile);
            return profile;
        }

        public static ClientBuildProfile Detect(string gameDirectory)
        {
            if (string.IsNullOrWhiteSpace(gameDirectory))
                return ClientBuildProfiles.Modern;

            string executablePath = Path.Combine(gameDirectory, "KartRider.exe");
            string executableHash = TryComputeSha256(executablePath);

            if (string.Equals(executableHash, ClientBuildProfiles.Korean5136.ExecutableSha256, StringComparison.OrdinalIgnoreCase))
                return ClientBuildProfiles.Korean5136;
            if (string.Equals(executableHash, ClientBuildProfiles.Korean20051214.ExecutableSha256, StringComparison.OrdinalIgnoreCase))
                return ClientBuildProfiles.Korean20051214;

            string pinPath = Path.Combine(gameDirectory, "KartRider.pin");
            if (TryReadShallowPinHeader(pinPath, out ShallowPinHeader header))
            {
                if (MatchesLegacyPin(ClientBuildProfiles.Korean20051214, header))
                    return ClientBuildProfiles.Korean20051214;
                if (MatchesLegacyPin(ClientBuildProfiles.Korean5136, header))
                    return ClientBuildProfiles.Korean5136;
            }

            // A successful full parse confirms a modern PIN. Unknown or absent PIN files
            // also retain the historical Modern fallback so existing installs are unchanged.
            try
            {
                if (File.Exists(pinPath))
                    _ = new PINFile(pinPath);
            }
            catch
            {
            }

            return ClientBuildProfiles.Modern;
        }

        private static bool MatchesLegacyPin(ClientBuildProfile profile, ShallowPinHeader header)
        {
            return profile.PinVersion.HasValue &&
                   header.MinorVersion == profile.PinVersion.Value &&
                   header.LocaleId == profile.LocaleId &&
                   header.ClientLocation == profile.ClientLocation;
        }

        public static bool TryReadShallowPinHeader(string pinPath, out ShallowPinHeader header)
        {
            header = default;
            try
            {
                if (!File.Exists(pinPath))
                    return false;

                InPacket envelope = new InPacket(File.ReadAllBytes(pinPath));
                int encodedLength = envelope.ReadInt();
                if (encodedLength <= 0 || encodedLength > envelope.Available)
                    return false;

                byte[] decoded = KREncodedBlock.Decode(envelope.ReadBytes(encodedLength));
                InPacket pin = new InPacket(decoded);
                if (pin.ReadUInt() != PinMagic)
                    return false;

                pin.ReadByte();
                ushort localeId = pin.ReadUShort();
                ushort clientLocation = pin.ReadUShort();
                pin.ReadByte();
                pin.ReadByte();
                ushort minorVersion = pin.ReadUShort();
                header = new ShallowPinHeader(localeId, clientLocation, minorVersion);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static string TryComputeSha256(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return string.Empty;

                using FileStream stream = File.OpenRead(path);
                using SHA256 sha256 = SHA256.Create();
                return Convert.ToHexString(sha256.ComputeHash(stream));
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    public sealed class ClientLaunchContext
    {
        public string GameDirectory { get; }
        public string PinFile { get; }
        public string PinBackupFile { get; }
        public string ServerIp { get; }
        public ushort ServerPort { get; }
        public string Username { get; }

        public ClientLaunchContext(string gameDirectory, string pinFile, string pinBackupFile, string serverIp, ushort serverPort, string username)
        {
            GameDirectory = Path.GetFullPath(gameDirectory);
            PinFile = pinFile;
            PinBackupFile = pinBackupFile;
            ServerIp = serverIp;
            ServerPort = serverPort;
            Username = username ?? string.Empty;
        }
    }

    public interface IClientLaunchStrategy
    {
        void Launch(ClientLaunchContext context);
        void Restore(ClientLaunchContext context);
    }

    public static class ClientLaunchStrategies
    {
        private static readonly IClientLaunchStrategy ModernStrategy = new ModernPinLaunchStrategy();
        private static readonly IClientLaunchStrategy LegacyStrategy = new LegacyProfileLaunchStrategy();

        public static IClientLaunchStrategy For(ClientBuildProfile profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));
            return profile.UsesModernPin ? ModernStrategy : LegacyStrategy;
        }
    }

    internal sealed class ModernPinLaunchStrategy : IClientLaunchStrategy
    {
        public void Launch(ClientLaunchContext context)
        {
            new MemoryModifier().LaunchAndModifyMemory(context.GameDirectory, context.PinFile, context.PinBackupFile);
        }

        public void Restore(ClientLaunchContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.PinBackupFile) ||
                string.IsNullOrWhiteSpace(context.PinFile) || !File.Exists(context.PinBackupFile))
            {
                return;
            }

            try
            {
                if (File.Exists(context.PinFile))
                    File.Delete(context.PinFile);
                File.Move(context.PinBackupFile, context.PinFile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PIN 파일 복원 실패: {ex.Message}");
            }
        }
    }

    internal sealed class LegacyProfileLaunchStrategy : IClientLaunchStrategy
    {
        private const string BackupSuffix = ".launcher-v2.bak";
        private const string CreatedSuffix = ".launcher-v2.created";
        private const string TemporarySuffix = ".launcher-v2.tmp";
        private const string PristineBackupSuffix = ".pristine.bak";
        private const string PristineAbsentSuffix = ".pristine.absent";

        public void Launch(ClientLaunchContext context)
        {
            ClientBuildProfile profile = ClientBuildProfiles.Active;
            if (!profile.IsLegacy)
                throw new InvalidOperationException("구형 실행 방식에는 구형 클라이언트 프로필이 필요합니다.");

            string gameConfigPath = Path.Combine(context.GameDirectory, "KartRider.xml");
            string launcherProfilePath = Path.Combine(context.GameDirectory, profile.ProfileRelativePath);
            try
            {
                ushort loginPort = profile.Ports.ResolveLoginTcpPort(context.ServerPort);
                string clientServerIp = LanIpGetter.IsIPv6(context.ServerIp)
                    ? "127.0.0.1"
                    : context.ServerIp;
                if (profile.Build == ClientBuild.Korean20051214)
                    PrepareLegacyPin(context.PinFile, clientServerIp, loginPort);
                if (profile.Build == ClientBuild.Korean5136)
                {
                    PrepareKorean5136Pin(context.PinFile, clientServerIp, loginPort);
                    PrepareKorean5136GameConfig(gameConfigPath, clientServerIp, loginPort);
                    PrepareKorean5136LauncherProfile(launcherProfilePath, context.Username);
                }
                else
                {
                    PrepareGameConfig(gameConfigPath, clientServerIp, loginPort);
                    PrepareLauncherProfile(launcherProfilePath, context.Username);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo(Path.Combine(context.GameDirectory, "KartRider.exe"), "-profile:launcher")
                {
                    WorkingDirectory = context.GameDirectory,
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process process = new Process
                {
                    StartInfo = startInfo
                };

                if (!process.Start())
                {
                    process.Dispose();
                    throw new InvalidOperationException($"{profile.DisplayName} 실행에 실패했습니다.");
                }

                int processId = process.Id;
                process.Exited += (_, _) =>
                {
                    Restore(context);
                    process.Dispose();
                };
                process.EnableRaisingEvents = true;
                Console.WriteLine($"{profile.DisplayName} 실행 완료: 프로세스 ID {processId}");
            }
            catch
            {
                Restore(context);
                throw;
            }
        }

        public void Restore(ClientLaunchContext context)
        {
            if (context == null || string.IsNullOrWhiteSpace(context.GameDirectory))
                return;

            ClientBuildProfile profile = ClientBuildProfiles.Active;
            // P5136 keeps the selected endpoint in the live files. Its stock
            // files are preserved once as *.pristine.bak instead of being
            // restored while the protected executable is still starting.
            if (profile.Build == ClientBuild.Korean5136)
                return;

            if (!string.IsNullOrWhiteSpace(context.PinFile))
                RestoreFile(context.PinFile);
            RestoreFile(Path.Combine(context.GameDirectory, "KartRider.xml"));
            if (!string.IsNullOrWhiteSpace(profile.ProfileRelativePath))
                RestoreFile(Path.Combine(context.GameDirectory, profile.ProfileRelativePath));
        }

        private static void PrepareGameConfig(
            string path,
            string serverIp,
            ushort serverPort)
        {
            XDocument document;
            if (File.Exists(path))
            {
                document = LoadLegacyXml(path);
                if (document.Root == null || !string.Equals(document.Root.Name.LocalName, "config", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("KartRider.xml에 config 루트 요소가 없습니다.");
            }
            else
            {
                document = new XDocument(new XDeclaration("1.0", "utf-16", null), new XElement("config"));
            }

            XElement server = null;
            foreach (XElement element in document.Root.Elements())
            {
                if (string.Equals(element.Name.LocalName, "server", StringComparison.OrdinalIgnoreCase))
                {
                    server = element;
                    break;
                }
            }

            if (server == null)
            {
                server = new XElement("server");
                document.Root.AddFirst(server);
            }
            server.SetAttributeValue("addr", FormatEndpoint(serverIp, serverPort));

            SnapshotFile(path);
            SaveUtf16Atomically(path, document);
        }

        internal static void PrepareKorean5136GameConfig(
            string path,
            string serverIp,
            ushort serverPort)
        {
            // P5136 reads UTF-8 bytes without a BOM even though the declaration
            // says UTF-16. Preserve that client-facing format.
            string content =
                "<?xml version='1.0' encoding='UTF-16'?>\r\n" +
                "<config>\r\n" +
                $"\t<server addr='{FormatEndpoint(serverIp, serverPort)}'/>\r\n" +
                "</config>";

            PreparePersistentFile(path, required: false);
            SaveUtf8NoBomAtomically(path, content);
        }

        internal static void PrepareKorean5136LauncherProfile(string path, string username)
        {
            string usernameElement = new XElement("username", username ?? string.Empty)
                .ToString(SaveOptions.DisableFormatting);
            string content =
                "<?xml version='1.0' encoding='UTF-16'?>\r\n" +
                "<profile>\r\n" +
                usernameElement + "\r\n" +
                "</profile>";

            PreparePersistentFile(path, required: false);
            SaveUtf8NoBomAtomically(path, content);
        }

        internal static void PrepareKorean5136Pin(string path, string serverIp, ushort serverPort)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new FileNotFoundException("P5136 KartRider.pin 파일을 찾을 수 없습니다.", path);

            // On the first persistent launch only, promote a transient backup
            // left by an older connector to the immutable pristine backup.
            PreparePersistentFile(path, required: true);

            PINFile pin = new PINFile(path);
            if (pin.Header.MinorVersion != 5136)
                throw new InvalidDataException($"지원하지 않는 P5136 PIN 프로토콜: {pin.Header.MinorVersion}");
            if (pin.AuthMethods == null || pin.AuthMethods.Count == 0)
                throw new InvalidDataException("P5136 PIN에 인증 방식이 없습니다.");

            foreach (PINFile.AuthMethod authMethod in pin.AuthMethods)
            {
                if (authMethod == null)
                    throw new InvalidDataException("P5136 PIN에 올바르지 않은 인증 방식이 있습니다.");

                if (authMethod.LoginServers == null)
                    authMethod.LoginServers = new List<PINFile.IPEndPoint>();
                else
                    authMethod.LoginServers.Clear();

                authMethod.LoginServers.Add(new PINFile.IPEndPoint
                {
                    IP = serverIp,
                    Port = serverPort
                });
            }

            if (!ProfileService.SettingConfig.NgsOn && pin.BmlObjects != null)
            {
                foreach (BmlObject bml in pin.BmlObjects)
                {
                    if (!string.Equals(bml?.Name, "extra", StringComparison.OrdinalIgnoreCase) ||
                        bml.SubObjects == null)
                    {
                        continue;
                    }

                    for (int i = bml.SubObjects.Count - 1; i >= 0; i--)
                    {
                        if (string.Equals(bml.SubObjects[i].Item1, "NgsOn", StringComparison.OrdinalIgnoreCase))
                            bml.SubObjects.RemoveAt(i);
                    }
                }
            }

            byte[] patchedPin = pin.GetEncryptedData();
            string temporaryPath = path + TemporarySuffix;
            try
            {
                File.WriteAllBytes(temporaryPath, patchedPin);
                VerifyKorean5136PinEndpoint(temporaryPath, serverIp, serverPort);
                File.Move(temporaryPath, path, true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }

            Console.WriteLine(
                $"P5136 PIN 로그인 주소 준비 완료: {serverIp}:{serverPort} " +
                $"(인증 방식 {pin.AuthMethods.Count}개)");
        }

        private static void PreparePersistentFile(string path, bool required)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            string backupPath = path + PristineBackupSuffix;
            string absentPath = path + PristineAbsentSuffix;
            bool hasBackup = File.Exists(backupPath);
            bool wasAbsent = File.Exists(absentPath);
            if (hasBackup && wasAbsent)
            {
                throw new InvalidDataException(
                    $"{path}의 순정 상태 파일이 서로 충돌합니다.");
            }

            if (!hasBackup && !wasAbsent)
            {
                string legacyBackupPath = path + BackupSuffix;
                string legacyCreatedPath = path + CreatedSuffix;
                if (File.Exists(legacyBackupPath))
                {
                    CreatePristineBackupOnce(legacyBackupPath, backupPath);
                    File.Copy(legacyBackupPath, path, true);
                    File.Delete(legacyBackupPath);
                    if (File.Exists(legacyCreatedPath))
                        File.Delete(legacyCreatedPath);
                }
                else if (File.Exists(legacyCreatedPath))
                {
                    if (required)
                    {
                        throw new FileNotFoundException(
                            "원본 P5136 PIN이 없던 상태로 기록되어 있습니다.",
                            path);
                    }
                    CreatePristineAbsentMarkerOnce(absentPath);
                    File.Delete(legacyCreatedPath);
                }
                else if (File.Exists(path))
                {
                    CreatePristineBackupOnce(path, backupPath);
                }
                else if (required)
                {
                    throw new FileNotFoundException(
                        "P5136 KartRider.pin 파일을 찾을 수 없습니다.",
                        path);
                }
                else
                {
                    CreatePristineAbsentMarkerOnce(absentPath);
                }
            }

            string legacyTemporaryPath = path + TemporarySuffix;
            if (File.Exists(legacyTemporaryPath))
                File.Delete(legacyTemporaryPath);

            if (required && !File.Exists(path))
            {
                if (File.Exists(backupPath))
                    File.Copy(backupPath, path, false);
                else
                    throw new FileNotFoundException("P5136 KartRider.pin 파일을 찾을 수 없습니다.", path);
            }
        }

        private static void CreatePristineBackupOnce(string sourcePath, string backupPath)
        {
            try
            {
                File.Copy(sourcePath, backupPath, false);
            }
            catch (IOException) when (File.Exists(backupPath))
            {
                // Another connector instance won the create-once race.
            }
        }

        private static void CreatePristineAbsentMarkerOnce(string absentPath)
        {
            try
            {
                using FileStream marker = new FileStream(
                    absentPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.Read);
            }
            catch (IOException) when (File.Exists(absentPath))
            {
                // Another connector instance recorded the pristine absence.
            }
        }

        private static void VerifyKorean5136PinEndpoint(
            string path,
            string expectedServerIp,
            ushort expectedServerPort)
        {
            PINFile verified = new PINFile(path);
            if (verified.Header.MinorVersion != 5136 ||
                verified.AuthMethods == null ||
                verified.AuthMethods.Count == 0)
            {
                throw new InvalidDataException("수정한 P5136 PIN을 검증하지 못했습니다.");
            }

            foreach (PINFile.AuthMethod authMethod in verified.AuthMethods)
            {
                if (authMethod?.LoginServers == null || authMethod.LoginServers.Count != 1)
                    throw new InvalidDataException("수정한 P5136 PIN의 로그인 주소 목록이 올바르지 않습니다.");

                PINFile.IPEndPoint endpoint = authMethod.LoginServers[0];
                if (!string.Equals(endpoint.IP, expectedServerIp, StringComparison.OrdinalIgnoreCase) ||
                    endpoint.Port != expectedServerPort)
                {
                    throw new InvalidDataException(
                        $"수정한 P5136 PIN 주소는 {endpoint}이며, 예상 주소는 " +
                        $"{expectedServerIp}:{expectedServerPort}입니다.");
                }
            }
        }

        private static void PrepareLegacyPin(string path, string serverIp, ushort serverPort)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                throw new FileNotFoundException("2005 KartRider.pin 파일을 찾을 수 없습니다.", path);

            // A previous launcher or client crash may have left the temporary
            // v2 PIN in place. Always recover the original v1 file before
            // constructing the next launch image.
            RecoverPreviousLaunch(path);

            PINFile pin = new PINFile(path);
            if (pin.Header.Unk1 != 1 && pin.Header.Unk1 != 2)
                throw new InvalidDataException($"지원하지 않는 2005 PIN 형식: {pin.Header.Unk1}");

            pin.Header.Unk1 = 2;
            pin.Header.LoginType = 2;
            pin.Header.AESKey = string.Empty;
            pin.Header.PatchURL = string.Empty;

            PINFile.AuthMethod authMethod = new PINFile.AuthMethod
            {
                Index = 1,
                Name = "Default",
                AccountConfig = null,
                ExtraConfig = null
            };
            authMethod.LoginServers.Add(new PINFile.IPEndPoint
            {
                IP = serverIp,
                Port = serverPort
            });
            pin.AuthMethods.Clear();
            pin.AuthMethods.Add(authMethod);

            BmlObject document = new BmlObject { Name = "document" };
            document.SetKeyValuePair("root", "카트라이더_236");
            document.SetKeyValuePair("screenShot", "스크린샷");
            document.SetKeyValuePair("riderData", "라이더데이터");

            BmlObject storage = new BmlObject { Name = "storage" };
            storage.SubObjects.Add(Tuple.Create("document", document));

            pin.StorageConfig = storage;
            pin.ExtraConfig = null;
            pin.BmlObjects.Clear();
            pin.BmlObjects.Add(storage);

            byte[] convertedPin = pin.GetEncryptedData();
            SnapshotFile(path);
            SaveBytesAtomically(path, convertedPin);
        }

        private static void SaveBytesAtomically(string path, byte[] data)
        {
            string temporaryPath = path + TemporarySuffix;
            File.WriteAllBytes(temporaryPath, data);
            File.Move(temporaryPath, path, true);
        }

        private static void SaveUtf8NoBomAtomically(string path, string content)
        {
            string temporaryPath = path + TemporarySuffix;
            File.WriteAllText(temporaryPath, content, new UTF8Encoding(false));
            File.Move(temporaryPath, path, true);
        }

        private static void PrepareLauncherProfile(string path, string username)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            XDocument document;
            if (File.Exists(path))
            {
                document = LoadLegacyXml(path);
                if (document.Root == null || !string.Equals(document.Root.Name.LocalName, "profile", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("launcher.xml에 profile 루트 요소가 없습니다.");
            }
            else
            {
                document = new XDocument(new XDeclaration("1.0", "utf-16", null), new XElement("profile"));
            }

            XElement usernameElement = null;
            foreach (XElement element in document.Root.Elements())
            {
                if (string.Equals(element.Name.LocalName, "username", StringComparison.OrdinalIgnoreCase))
                {
                    usernameElement = element;
                    break;
                }
            }

            if (usernameElement == null)
            {
                usernameElement = new XElement("username");
                document.Root.Add(usernameElement);
            }
            usernameElement.Value = username;

            SnapshotFile(path);
            SaveUtf16Atomically(path, document);
        }

        private static XDocument LoadLegacyXml(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Encoding encoding;
            int preambleLength;

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                encoding = new UTF8Encoding(false, true);
                preambleLength = 3;
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                encoding = Encoding.Unicode;
                preambleLength = 2;
            }
            else if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                encoding = Encoding.BigEndianUnicode;
                preambleLength = 2;
            }
            else if (LooksLikeUtf16(bytes, littleEndian: true))
            {
                encoding = Encoding.Unicode;
                preambleLength = 0;
            }
            else if (LooksLikeUtf16(bytes, littleEndian: false))
            {
                encoding = Encoding.BigEndianUnicode;
                preambleLength = 0;
            }
            else
            {
                // Several original Korean clients declare UTF-16 while shipping
                // BOM-less single-byte XML. Treat those bytes as UTF-8 first.
                encoding = new UTF8Encoding(false, true);
                preambleLength = 0;
            }

            string xml;
            try
            {
                xml = encoding.GetString(bytes, preambleLength, bytes.Length - preambleLength);
            }
            catch (DecoderFallbackException)
            {
                // The fields we edit are ASCII. Latin-1 preserves every other byte
                // losslessly enough for the XML parser instead of rejecting the file.
                xml = Encoding.Latin1.GetString(bytes);
            }

            return XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }

        private static bool LooksLikeUtf16(byte[] bytes, bool littleEndian)
        {
            int sampleLength = Math.Min(bytes.Length, 128);
            if (sampleLength < 4)
                return false;

            int expectedNulls = 0;
            int expectedSlots = 0;
            for (int i = littleEndian ? 1 : 0; i < sampleLength; i += 2)
            {
                expectedSlots++;
                if (bytes[i] == 0)
                    expectedNulls++;
            }

            return expectedNulls >= Math.Max(2, expectedSlots * 3 / 4);
        }

        private static void SnapshotFile(string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            RecoverPreviousLaunch(path);

            string backupPath = path + BackupSuffix;
            string createdPath = path + CreatedSuffix;
            if (File.Exists(path))
            {
                File.Copy(path, backupPath, true);
            }
            else
            {
                File.WriteAllText(createdPath, string.Empty, new UTF8Encoding(false));
            }
        }

        private static void RecoverPreviousLaunch(string path)
        {
            string backupPath = path + BackupSuffix;
            string createdPath = path + CreatedSuffix;
            if (File.Exists(backupPath))
            {
                if (File.Exists(path))
                    File.Delete(path);
                File.Move(backupPath, path);
            }
            else if (File.Exists(createdPath))
            {
                if (File.Exists(path))
                    File.Delete(path);
                File.Delete(createdPath);
            }

            string temporaryPath = path + TemporarySuffix;
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        private static void RestoreFile(string path)
        {
            try
            {
                RecoverPreviousLaunch(path);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{path} 복원 실패: {ex.Message}");
            }
        }

        private static void SaveUtf16Atomically(string path, XDocument document)
        {
            string temporaryPath = path + TemporarySuffix;
            XmlWriterSettings settings = new XmlWriterSettings
            {
                Encoding = Encoding.Unicode,
                Indent = true,
                OmitXmlDeclaration = false
            };

            using (XmlWriter writer = XmlWriter.Create(temporaryPath, settings))
                document.Save(writer);

            File.Move(temporaryPath, path, true);
        }

        private static string FormatEndpoint(string serverIp, ushort serverPort)
        {
            string host = string.IsNullOrWhiteSpace(serverIp) ? "127.0.0.1" : serverIp.Trim();
            if (host.Contains(':') && !host.StartsWith("[", StringComparison.Ordinal))
                host = $"[{host}]";
            return string.Create(CultureInfo.InvariantCulture, $"{host}:{serverPort}");
        }
    }
}
