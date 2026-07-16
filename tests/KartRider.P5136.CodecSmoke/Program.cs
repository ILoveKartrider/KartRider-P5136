using KartLibrary.Data;
using KartRider.Common.Data;
using System;
using System.Linq;
using System.Text;

byte[] source = Encoding.UTF8.GetBytes(string.Concat(
    Enumerable.Repeat("P5136 framework zlib round-trip payload. ", 256)));

foreach ((bool encrypted, bool compressed) in new[]
{
    (false, false),
    (false, true),
    (true, false),
    (true, true)
})
{
    byte[] encoded = DataProcessor.EncodeKRData(
        source,
        encrypted,
        compressed,
        0x51365136u);
    byte[] decoded = DataProcessor.DecodeKRData(encoded);
    if (!source.SequenceEqual(decoded))
    {
        Console.Error.WriteLine(
            $"KRData round trip failed: encrypted={encrypted}, compressed={compressed}");
        return 1;
    }
}

Console.WriteLine("KRData codec smoke test passed (4 modes).");

if (args.Length == 1)
{
    PINFile pin = new PINFile(args[0]);
    if (pin.Header.MinorVersion != 5136)
    {
        Console.Error.WriteLine($"Unexpected PIN protocol: {pin.Header.MinorVersion}");
        return 1;
    }
    Console.WriteLine("P5136 PIN read smoke test passed.");
}

return 0;
