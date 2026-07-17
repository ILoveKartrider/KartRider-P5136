using KartRider.Common.Utilities;
using KartRider.IO.Packet;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KartRider.Compatibility;

internal static class Korean5136LoginProfileReader
{
    private const int MaximumDepth = 8;
    private const int MaximumNodes = 64;
    private const int MaximumAttributesPerNode = 64;
    private const int MaximumChildrenPerNode = 64;
    private const int MaximumStringLength = 1024;

    private static readonly uint AccountDataProfileHash =
        Adler32Helper.GenerateAdler32_ASCII("AccountDataProfile", 0);

    public static string ReadUsername(InPacket packet)
    {
        if (packet == null)
            throw new ArgumentNullException(nameof(packet));
        if (packet.Available < 13)
            throw new FormatException("PqLogin does not contain an AccountDataProfile payload.");

        // The first two values vary for every P5136 login. The third value is
        // the nested packet hash, followed by a reserved byte and BML data.
        packet.ReadUInt();
        packet.ReadUInt();
        uint nestedPacketHash = packet.ReadUInt();
        if (nestedPacketHash != AccountDataProfileHash)
        {
            throw new FormatException(
                $"PqLogin nested packet 0x{nestedPacketHash:X8} is not AccountDataProfile.");
        }

        byte reserved = packet.ReadByte();
        if (reserved != 0)
            throw new FormatException($"PqLogin AccountDataProfile reserved byte is {reserved}, not zero.");

        int nodeCount = 0;
        string username = null;
        string rootName = ReadNode(packet, 0, ref nodeCount, ref username);
        if (!string.Equals(rootName, "profile", StringComparison.OrdinalIgnoreCase))
            throw new FormatException($"AccountDataProfile root is '{rootName}', not 'profile'.");
        if (string.IsNullOrWhiteSpace(username))
            throw new FormatException("AccountDataProfile does not contain a username value.");

        return username.Trim();
    }

    private static string ReadNode(
        InPacket packet,
        int depth,
        ref int nodeCount,
        ref string username)
    {
        if (depth > MaximumDepth)
            throw new FormatException($"BML nesting exceeds {MaximumDepth} levels.");
        if (++nodeCount > MaximumNodes)
            throw new FormatException($"BML contains more than {MaximumNodes} nodes.");

        string name = ReadLimitedString(packet, "node name");
        string value = ReadLimitedString(packet, $"value for '{name}'");

        int attributeCount = ReadCount(packet, MaximumAttributesPerNode, "attribute");
        for (int index = 0; index < attributeCount; index++)
        {
            ReadLimitedString(packet, $"attribute {index} name");
            ReadLimitedString(packet, $"attribute {index} value");
        }

        if (username == null && string.Equals(name, "username", StringComparison.OrdinalIgnoreCase))
            username = value;

        int childCount = ReadCount(packet, MaximumChildrenPerNode, "child");
        for (int index = 0; index < childCount; index++)
            ReadNode(packet, depth + 1, ref nodeCount, ref username);

        return name;
    }

    private static int ReadCount(InPacket packet, int maximum, string kind)
    {
        int count = packet.ReadInt();
        if (count < 0 || count > maximum)
            throw new FormatException($"BML {kind} count {count} is outside 0..{maximum}.");
        return count;
    }

    private static string ReadLimitedString(InPacket packet, string field)
    {
        int characterCount = packet.ReadInt();
        if (characterCount < 0 || characterCount > MaximumStringLength)
        {
            throw new FormatException(
                $"BML {field} length {characterCount} is outside 0..{MaximumStringLength} characters.");
        }

        return Encoding.Unicode.GetString(packet.ReadBytes(checked(characterCount * 2)));
    }
}

public static class ClientIdentityValidator
{
    public const int MaximumNicknameLength = 32;

    private static readonly HashSet<string> ReservedWindowsNames = new HashSet<string>(
        new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        },
        StringComparer.OrdinalIgnoreCase);

    public static bool TryNormalize(
        string value,
        string profileRoot,
        out string nickname,
        out string error)
    {
        nickname = value?.Trim() ?? string.Empty;
        error = string.Empty;

        if (nickname.Length == 0)
        {
            error = "계정 이름이 비어 있습니다.";
            return false;
        }
        if (nickname.Length > MaximumNicknameLength)
        {
            error = $"계정 이름은 {MaximumNicknameLength}자를 넘을 수 없습니다.";
            return false;
        }
        if (nickname == "." || nickname == ".." ||
            nickname.EndsWith(".", StringComparison.Ordinal) ||
            nickname.EndsWith(" ", StringComparison.Ordinal))
        {
            error = "계정 이름에 사용할 수 없는 경로 형식이 포함되어 있습니다.";
            return false;
        }

        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        foreach (char character in nickname)
        {
            if (char.IsControl(character) || character == '/' || character == '\\' ||
                Array.IndexOf(invalidCharacters, character) >= 0)
            {
                error = $"계정 이름에 사용할 수 없는 문자 U+{(int)character:X4}가 포함되어 있습니다.";
                return false;
            }
        }

        string baseName = nickname.Split('.')[0];
        if (ReservedWindowsNames.Contains(baseName))
        {
            error = "Windows 예약 이름은 계정 이름으로 사용할 수 없습니다.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(profileRoot))
        {
            string root = Path.GetFullPath(profileRoot);
            string rootPrefix = root.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string candidate = Path.GetFullPath(Path.Combine(root, nickname));
            if (!candidate.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            {
                error = "계정 프로필 경로가 Profile 디렉터리를 벗어납니다.";
                return false;
            }
        }

        return true;
    }
}
