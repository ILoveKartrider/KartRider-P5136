using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KartRider;

internal sealed class P5136ItemSymbolScanResult
{
    public bool Success => RequestedCount > 0 && ResolvedCount == RequestedCount;

    public int RequestedCount { get; init; }

    public int ResolvedCount => Mappings.Count;

    public double Coverage => RequestedCount == 0
        ? 0
        : (double)ResolvedCount / RequestedCount;

    public IReadOnlyDictionary<string, short> Mappings { get; init; } =
        new Dictionary<string, short>(StringComparer.Ordinal);

    public IReadOnlyList<string> MissingSymbols { get; init; } = Array.Empty<string>();

    public IReadOnlyDictionary<string, string> SymbolErrors { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public string Error { get; init; } = string.Empty;
}

internal static class P5136ItemSymbolScanner
{
    private const ushort DosSignature = 0x5A4D;
    private const uint PeSignature = 0x00004550;
    private const ushort I386Machine = 0x014C;
    private const ushort Pe32Magic = 0x010B;
    private const uint SectionMemoryExecute = 0x20000000;
    private const int MaximumSections = 96;
    private const int MaximumSymbolLength = 128;

    public static P5136ItemSymbolScanResult Scan(
        string executablePath,
        IEnumerable<string> requiredSymbols)
    {
        string[] symbols;
        try
        {
            symbols = NormalizeSymbols(requiredSymbols);
        }
        catch (Exception ex)
        {
            return Failure(0, ex.Message);
        }

        if (symbols.Length == 0)
        {
            return Failure(0, "at least one item symbol is required");
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return Failure(symbols.Length, "unpacked executable path is required");
        }

        byte[] image;
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(executablePath);
            if (!File.Exists(fullPath))
            {
                return Failure(
                    symbols.Length,
                    $"unpacked executable was not found: {fullPath}");
            }

            image = File.ReadAllBytes(fullPath);
        }
        catch (Exception ex) when (
            ex is IOException ||
            ex is UnauthorizedAccessException ||
            ex is ArgumentException ||
            ex is NotSupportedException)
        {
            return Failure(symbols.Length, ex.Message);
        }

        if (!TryReadPeImage(image, out PeImage pe, out string peError))
        {
            return Failure(symbols.Length, peError);
        }

        var mappings = new SortedDictionary<string, short>(StringComparer.Ordinal);
        var missing = new List<string>();
        var symbolErrors = new SortedDictionary<string, string>(StringComparer.Ordinal);
        int symbolsFoundInImage = 0;

        foreach (string symbol in symbols)
        {
            SymbolResolution resolution = ResolveSymbol(image, pe, symbol);
            if (resolution.StringOccurrenceCount > 0)
            {
                symbolsFoundInImage++;
            }

            if (resolution.Ids.Count == 1)
            {
                mappings[symbol] = resolution.Ids[0];
                continue;
            }

            missing.Add(symbol);
            symbolErrors[symbol] = resolution.Error;
        }

        string error = string.Empty;
        if (mappings.Count != symbols.Length)
        {
            error = symbolsFoundInImage == 0
                ? $"none of the {symbols.Length} requested UTF-16 item symbols were found; " +
                  "an unpacked KartRiderU.exe is required"
                : $"item symbol coverage is {mappings.Count}/{symbols.Length}; " +
                  $"unresolved: {string.Join(", ", missing.Take(12))}" +
                  (missing.Count > 12 ? $" (+{missing.Count - 12} more)" : string.Empty);
        }

        return new P5136ItemSymbolScanResult
        {
            RequestedCount = symbols.Length,
            Mappings = mappings,
            MissingSymbols = missing,
            SymbolErrors = symbolErrors,
            Error = error
        };
    }

    private static SymbolResolution ResolveSymbol(byte[] image, PeImage pe, string symbol)
    {
        byte[] encoded = Encoding.Unicode.GetBytes(symbol + "\0");
        var ids = new SortedSet<short>();
        int occurrenceCount = 0;

        foreach (PeSection dataSection in pe.Sections)
        {
            ReadOnlySpan<byte> sectionData = image.AsSpan(
                dataSection.RawOffset,
                dataSection.RawLength);
            int searchOffset = 0;
            while (searchOffset <= sectionData.Length - encoded.Length)
            {
                int relative = sectionData.Slice(searchOffset).IndexOf(encoded);
                if (relative < 0)
                {
                    break;
                }

                relative += searchOffset;
                int fileOffset = checked(dataSection.RawOffset + relative);
                occurrenceCount++;
                if (TryFileOffsetToVirtualAddress(
                        pe,
                        dataSection,
                        fileOffset,
                        out uint stringAddress))
                {
                    ResolveAddressReferences(image, pe, stringAddress, ids);
                }

                searchOffset = relative + 2;
            }
        }

        string error = ids.Count switch
        {
            0 when occurrenceCount == 0 => "UTF-16 symbol was not found in PE sections",
            0 => "symbol was found, but no validated item-id initializer reference was found",
            1 => string.Empty,
            _ => $"ambiguous item ids: {string.Join(", ", ids)}"
        };
        return new SymbolResolution(occurrenceCount, ids.ToArray(), error);
    }

    private static void ResolveAddressReferences(
        byte[] image,
        PeImage pe,
        uint stringAddress,
        ISet<short> ids)
    {
        Span<byte> pushedAddress = stackalloc byte[5];
        pushedAddress[0] = 0x68;
        BinaryPrimitives.WriteUInt32LittleEndian(pushedAddress.Slice(1), stringAddress);

        foreach (PeSection codeSection in pe.ExecutableSections)
        {
            ReadOnlySpan<byte> section = image.AsSpan(
                codeSection.RawOffset,
                codeSection.RawLength);
            int searchOffset = 0;
            while (searchOffset <= section.Length - pushedAddress.Length)
            {
                int relative = section.Slice(searchOffset).IndexOf(pushedAddress);
                if (relative < 0)
                {
                    break;
                }

                relative += searchOffset;
                int instructionOffset = checked(codeSection.RawOffset + relative);
                int sectionEnd = checked(codeSection.RawOffset + codeSection.RawLength);
                if (TryDecodeItemIdInitializer(
                        image,
                        instructionOffset + pushedAddress.Length,
                        sectionEnd,
                        out short id))
                {
                    ids.Add(id);
                }

                searchOffset = relative + 1;
            }
        }
    }

    private static bool TryDecodeItemIdInitializer(
        byte[] image,
        int position,
        int sectionEnd,
        out short id)
    {
        id = 0;
        if (!HasBytes(position, 7, sectionEnd, image.Length))
        {
            return false;
        }

        byte moveOpcode = image[position];
        if (moveOpcode < 0xB8 || moveOpcode > 0xBF ||
            BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(position + 1, 4)) != 4)
        {
            return false;
        }

        int sourceRegister = moveOpcode - 0xB8;
        position += 5;
        long decodedId;
        int arithmeticLength;
        switch (image[position])
        {
            case 0x6B:
            {
                if (!HasBytes(position, 3, sectionEnd, image.Length) ||
                    !UsesRegisterOperand(image[position + 1], sourceRegister))
                {
                    return false;
                }

                sbyte multiplier = unchecked((sbyte)image[position + 2]);
                if (multiplier < 0)
                {
                    return false;
                }
                decodedId = multiplier;
                arithmeticLength = 3;
                break;
            }
            case 0x69:
            {
                if (!HasBytes(position, 6, sectionEnd, image.Length) ||
                    !UsesRegisterOperand(image[position + 1], sourceRegister))
                {
                    return false;
                }

                decodedId = BinaryPrimitives.ReadInt32LittleEndian(
                    image.AsSpan(position + 2, 4));
                arithmeticLength = 6;
                break;
            }
            case 0xC1:
            {
                if (!HasBytes(position, 3, sectionEnd, image.Length) ||
                    !IsShiftLeftRegister(image[position + 1], sourceRegister))
                {
                    return false;
                }

                int shift = image[position + 2];
                if (shift > 14)
                {
                    return false;
                }
                decodedId = 1L << shift;
                arithmeticLength = 3;
                break;
            }
            case 0xD1:
            {
                if (!HasBytes(position, 2, sectionEnd, image.Length) ||
                    !IsShiftLeftRegister(image[position + 1], sourceRegister))
                {
                    return false;
                }

                decodedId = 2;
                arithmeticLength = 2;
                break;
            }
            default:
                return false;
        }

        if (decodedId < 0 || decodedId > short.MaxValue)
        {
            return false;
        }

        int tailStart = checked(position + arithmeticLength);
        if (!LooksLikeInitializerTail(image, tailStart, sectionEnd))
        {
            return false;
        }

        id = (short)decodedId;
        return true;
    }

    private static bool LooksLikeInitializerTail(byte[] image, int position, int sectionEnd)
    {
        int limit = Math.Min(Math.Min(sectionEnd, image.Length), position + 24);
        bool sawLea = false;
        for (int current = position; current < limit; current++)
        {
            if (image[current] == 0x8D)
            {
                sawLea = true;
            }
            else if (image[current] == 0xE8 && sawLea)
            {
                return true;
            }
        }
        return false;
    }

    private static bool UsesRegisterOperand(byte modRm, int sourceRegister)
    {
        return (modRm & 0xC0) == 0xC0 && (modRm & 0x07) == sourceRegister;
    }

    private static bool IsShiftLeftRegister(byte modRm, int sourceRegister)
    {
        return UsesRegisterOperand(modRm, sourceRegister) && ((modRm >> 3) & 0x07) == 4;
    }

    private static bool HasBytes(int position, int count, int sectionEnd, int imageLength)
    {
        return position >= 0 && count >= 0 &&
            position <= sectionEnd - count &&
            position <= imageLength - count;
    }

    private static bool TryFileOffsetToVirtualAddress(
        PeImage pe,
        PeSection section,
        int fileOffset,
        out uint address)
    {
        address = 0;
        long relative = (long)fileOffset - section.RawOffset;
        if (relative < 0 || relative >= section.RawLength)
        {
            return false;
        }

        long value = (long)pe.ImageBase + section.VirtualAddress + relative;
        if (value < 0 || value > uint.MaxValue)
        {
            return false;
        }

        address = (uint)value;
        return true;
    }

    private static bool TryReadPeImage(byte[] image, out PeImage pe, out string error)
    {
        pe = default;
        error = string.Empty;
        if (image.Length < 0x40 || ReadUInt16(image, 0) != DosSignature)
        {
            error = "file is not a valid DOS/PE image";
            return false;
        }

        uint peOffsetValue = ReadUInt32(image, 0x3C);
        if (peOffsetValue > int.MaxValue)
        {
            error = "PE header offset is outside the file";
            return false;
        }

        int peOffset = (int)peOffsetValue;
        if (!HasBytes(peOffset, 24, image.Length, image.Length) ||
            ReadUInt32(image, peOffset) != PeSignature)
        {
            error = "PE signature is missing or truncated";
            return false;
        }

        ushort machine = ReadUInt16(image, peOffset + 4);
        ushort sectionCount = ReadUInt16(image, peOffset + 6);
        ushort optionalHeaderSize = ReadUInt16(image, peOffset + 20);
        if (machine != I386Machine)
        {
            error = $"unsupported PE machine 0x{machine:X4}; expected 32-bit x86";
            return false;
        }
        if (sectionCount == 0 || sectionCount > MaximumSections)
        {
            error = $"invalid PE section count: {sectionCount}";
            return false;
        }

        int optionalHeader = checked(peOffset + 24);
        if (optionalHeaderSize < 32 ||
            !HasBytes(optionalHeader, optionalHeaderSize, image.Length, image.Length) ||
            ReadUInt16(image, optionalHeader) != Pe32Magic)
        {
            error = "unsupported or truncated PE32 optional header";
            return false;
        }
        uint imageBase = ReadUInt32(image, optionalHeader + 28);
        if (imageBase == 0)
        {
            error = "PE image base is zero";
            return false;
        }

        int sectionTable = checked(optionalHeader + optionalHeaderSize);
        int sectionTableLength = checked(sectionCount * 40);
        if (!HasBytes(sectionTable, sectionTableLength, image.Length, image.Length))
        {
            error = "PE section table is truncated";
            return false;
        }

        var sections = new List<PeSection>(sectionCount);
        for (int index = 0; index < sectionCount; index++)
        {
            int header = checked(sectionTable + index * 40);
            uint virtualAddress = ReadUInt32(image, header + 12);
            uint rawLengthValue = ReadUInt32(image, header + 16);
            uint rawOffsetValue = ReadUInt32(image, header + 20);
            uint characteristics = ReadUInt32(image, header + 36);
            if (rawLengthValue == 0)
            {
                continue;
            }
            if (rawLengthValue > int.MaxValue || rawOffsetValue > int.MaxValue)
            {
                error = $"PE section {index} raw range is too large";
                return false;
            }

            int rawLength = (int)rawLengthValue;
            int rawOffset = (int)rawOffsetValue;
            if (!HasBytes(rawOffset, rawLength, image.Length, image.Length))
            {
                error = $"PE section {index} raw range is outside the file";
                return false;
            }

            sections.Add(new PeSection(
                rawOffset,
                rawLength,
                virtualAddress,
                characteristics));
        }

        if (sections.Count == 0)
        {
            error = "PE image has no file-backed sections";
            return false;
        }

        PeSection[] executableSections = sections
            .Where(section => (section.Characteristics & SectionMemoryExecute) != 0)
            .ToArray();
        if (executableSections.Length == 0)
        {
            error = "PE image has no executable file-backed section";
            return false;
        }

        pe = new PeImage(imageBase, sections.ToArray(), executableSections);
        return true;
    }

    private static ushort ReadUInt16(byte[] image, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(offset, 2));
    }

    private static uint ReadUInt32(byte[] image, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(image.AsSpan(offset, 4));
    }

    private static string[] NormalizeSymbols(IEnumerable<string> requiredSymbols)
    {
        if (requiredSymbols == null)
        {
            throw new ArgumentNullException(nameof(requiredSymbols));
        }

        var symbols = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string value in requiredSymbols)
        {
            string symbol = value?.Trim();
            if (string.IsNullOrEmpty(symbol))
            {
                throw new ArgumentException("item symbols must not be empty");
            }
            if (symbol.Length > MaximumSymbolLength ||
                !IsAsciiLetter(symbol[0]) ||
                symbol.Any(character => !IsAsciiLetter(character) &&
                    !IsAsciiDigit(character) && character != '_'))
            {
                throw new ArgumentException($"invalid item symbol: {symbol}");
            }
            symbols.Add(symbol);
        }
        return symbols.ToArray();
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }

    private static bool IsAsciiDigit(char value)
    {
        return value is >= '0' and <= '9';
    }

    private static P5136ItemSymbolScanResult Failure(int requestedCount, string error)
    {
        return new P5136ItemSymbolScanResult
        {
            RequestedCount = requestedCount,
            Error = error
        };
    }

    private readonly record struct PeImage(
        uint ImageBase,
        PeSection[] Sections,
        PeSection[] ExecutableSections);

    private readonly record struct PeSection(
        int RawOffset,
        int RawLength,
        uint VirtualAddress,
        uint Characteristics);

    private readonly record struct SymbolResolution(
        int StringOccurrenceCount,
        IReadOnlyList<short> Ids,
        string Error);
}
