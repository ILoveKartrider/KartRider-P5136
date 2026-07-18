using KartLibrary.Data;
using KartRider.Common.Data;
using KartRider.Common.Network;
using KartRider.Common.Utilities;
using KartRider;
using KartRider.Compatibility;
using KartRider.IO.Packet;
using KartRider.ServerLauncher;
using KartRider_PacketName;
using ExcData;
using Profile;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

string randomTrackCatalogTestRoot =
    Environment.GetEnvironmentVariable("P5136_TRACK_CATALOG_ROOT");
if (!string.IsNullOrWhiteSpace(randomTrackCatalogTestRoot))
{
    Korean5136RandomTrackCatalog clientRandomCatalog =
        Korean5136RandomTrackService.LoadClientCatalog(randomTrackCatalogTestRoot);
    bool validChinaName = clientRandomCatalog.TryGetTrack(
        "china_R01",
        out Korean5136RandomTrackDefinition chinaTrack) &&
        chinaTrack.KoreanName == "차이나 서안 병마용";
    bool validReverseName = clientRandomCatalog.TryGetTrack(
        "china_R02_rvs",
        out Korean5136RandomTrackDefinition reverseTrack) &&
        reverseTrack.KoreanName.StartsWith("[리버스] ", StringComparison.Ordinal);
    bool validHot3Pools = clientRandomCatalog.TryGetPool(
        0,
        5,
        out Korean5136RandomTrackPool speedHot3) &&
        speedHot3.DefaultTrackIds.Count == 25 &&
        clientRandomCatalog.TryGetPool(
            1,
            5,
            out Korean5136RandomTrackPool itemHot3) &&
        itemHot3.DefaultTrackIds.Count == 25;
    bool validNewTrackPools = clientRandomCatalog.TryGetPool(
        0,
        8,
        out Korean5136RandomTrackPool speedNewTracks) &&
        speedNewTracks.DefaultTrackIds.Count == 20 &&
        clientRandomCatalog.TryGetPool(
            1,
            8,
            out Korean5136RandomTrackPool itemNewTracks) &&
        itemNewTracks.DefaultTrackIds.Count == 11;
    if (clientRandomCatalog.Tracks.Count < 280 ||
        clientRandomCatalog.Pools.Count < 20 ||
        !validChinaName ||
        !validReverseName ||
        !validHot3Pools ||
        !validNewTrackPools ||
        !clientRandomCatalog.TryGetPool(0, 33, out _) ||
        !clientRandomCatalog.TryGetPool(1, 33, out _) ||
        clientRandomCatalog.TryGetTrack("village_I13_rvs", out _) ||
        clientRandomCatalog.Tracks.Any(track =>
            track.KoreanName.Equals(track.Id, StringComparison.OrdinalIgnoreCase)))
    {
        Console.Error.WriteLine(
            $"P5136 client random-track catalog failed: " +
            $"tracks={clientRandomCatalog.Tracks.Count}, " +
            $"pools={clientRandomCatalog.Pools.Count}");
        return 1;
    }

    Korean5136RandomTrackService.Configure(
        randomTrackCatalogTestRoot,
        new RandomTrackConfiguration());
    ClientBuildProfile previousRandomTrackBuild = ClientBuildProfiles.Active;
    bool validClientSelection;
    try
    {
        ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean5136);
        validClientSelection =
            Korean5136RandomTrackService.TryGetCandidateHashes(
                0,
                5,
                basicAiOnly: false,
                out IReadOnlyList<uint> loadedHot3Hashes) &&
            loadedHot3Hashes.Count == 25 &&
            loadedHot3Hashes.Contains(RandomTrack.GetRandomTrack(
                null,
                "client-rho-random-smoke",
                0,
                5));
    }
    finally
    {
        ClientBuildProfiles.SetActive(previousRandomTrackBuild);
    }
    if (!validClientSelection)
    {
        Console.Error.WriteLine(
            "P5136 client random-track source-to-selection integration failed.");
        return 1;
    }

    Console.WriteLine(
        $"P5136 read-only client random-track catalog passed: " +
        $"tracks={clientRandomCatalog.Tracks.Count}, pools={clientRandomCatalog.Pools.Count}.");
}

string rhoCatalogTestRoot = Environment.GetEnvironmentVariable("P5136_RHO_CATALOG_ROOT");
if (!string.IsNullOrWhiteSpace(rhoCatalogTestRoot))
{
    string configuredCatalogOutput =
        Environment.GetEnvironmentVariable("P5136_RHO_CATALOG_OUTPUT");
    bool keepCatalogOutput = !string.IsNullOrWhiteSpace(configuredCatalogOutput);
    string catalogPath = keepCatalogOutput
        ? Path.GetFullPath(configuredCatalogOutput)
        : Path.Combine(
            Path.GetTempPath(),
            $"P5136-KartCatalog-{Environment.ProcessId}.xml");
    try
    {
        if (!KartRhoFile.TryExportKartCatalogXmlReadOnly(
                rhoCatalogTestRoot,
                catalogPath,
                out int exportedNameCount,
                out int exportedSpecCount,
                out string exportError))
        {
            Console.Error.WriteLine($"P5136 RHO kart catalog export failed: {exportError}");
            return 1;
        }

        XDocument exportedCatalog = XDocument.Load(catalogPath);
        XElement inventoryRoot = exportedCatalog.Root?.Element("Inventory");
        XElement itemSymbolsRoot = exportedCatalog.Root?.Element("ItemSymbols");
        XElement abilityRoot = exportedCatalog.Root?.Element("Abilities");
        bool HasItemSymbol(string name, string id) => itemSymbolsRoot?
            .Elements("Item")
            .Any(item =>
                item.Attribute("name")?.Value == name &&
                item.Attribute("id")?.Value == id &&
                !string.IsNullOrWhiteSpace(item.Attribute("evidence")?.Value)) == true;
        (string Name, string Id)[] hardcodedExecutableSymbols =
        {
            ("animalBooster", "31"), ("bigBanana", "85"),
            ("blockRocket", "117"), ("candyRocket", "102"),
            ("cokeBomb", "20"), ("cokeRocket", "30"),
            ("cokeRocketWorldCup", "39"), ("darkCloud", "1"),
            ("darkCloud2", "115"), ("dinoClawRocket", "108"),
            ("dinoEggRocket", "107"), ("drrMine", "23"),
            ("duckMine", "45"), ("eggMine", "82"),
            ("foxTailRocket", "126"), ("goldRocket", "32"),
            ("goldShield", "36"), ("infectedBomb", "27"),
            ("infectedWaterFly", "119"), ("lockdownRocket", "104"),
            ("prisonBomb", "47"), ("protectShield", "81"),
            ("pumpkinBomb", "44"), ("rainbowCloud", "43"),
            ("rollingCokeBomb", "22"), ("rollingInfectedBomb", "29"),
            ("sirenShield", "106"), ("snowBomb", "34"),
            ("snowWaterFly", "118"), ("snowman", "112"),
            ("tigerGhost", "101"), ("tigerRocket", "99"),
            ("timeCokeBomb", "21"), ("timeInfectedBomb", "28"),
            ("timeSnowBomb", "35"), ("waterMine", "37"),
            ("waterbombFly", "120")
        };
        bool hasAllHardcodedExecutableSymbols = hardcodedExecutableSymbols.All(expected =>
            itemSymbolsRoot?.Elements("Item").Any(item =>
                item.Attribute("name")?.Value == expected.Name &&
                item.Attribute("id")?.Value == expected.Id &&
                item.Attribute("evidence")?.Value ==
                    "P5136 verified executable supplement") == true);
        XElement goldenChickenBananaRule = abilityRoot?
            .Element("TransformByKart")?
            .Elements("Rule")
            .FirstOrDefault(rule =>
                rule.Attribute("kartId")?.Value == "1453" &&
                rule.Attribute("srcIdx")?.Value == "banana" &&
                rule.Attribute("dstIdx")?.Value == "goldEggMine" &&
                rule.Attribute("sourceId")?.Value == "8" &&
                rule.Attribute("targetId")?.Value == "83");
        XElement goldenChickenMagnetRule = abilityRoot?
            .Element("TransformByKart")?
            .Elements("Rule")
            .FirstOrDefault(rule =>
                rule.Attribute("kartId")?.Value == "1453" &&
                rule.Attribute("srcIdx")?.Value == "magnet" &&
                rule.Attribute("dstIdx")?.Value == "superMagnet" &&
                rule.Attribute("sourceId")?.Value == "5" &&
                rule.Attribute("targetId")?.Value == "103");
        XElement redLotusMagnetRule = abilityRoot?
            .Element("FiringToGain")?
            .Elements("Rule")
            .FirstOrDefault(rule =>
                rule.Attribute("kartId")?.Value == "1450" &&
                rule.Attribute("firingItemIdx")?.Value == "magnet" &&
                rule.Attribute("gainItemIdx")?.Value == "siren" &&
                rule.Attribute("probability")?.Value == "100" &&
                rule.Attribute("sourceId")?.Value == "5" &&
                rule.Attribute("targetId")?.Value == "24");
        XElement redLotusRocketRule = abilityRoot?
            .Element("FiringToGain")?
            .Elements("Rule")
            .FirstOrDefault(rule =>
                rule.Attribute("kartId")?.Value == "1450" &&
                rule.Attribute("firingItemIdx")?.Value == "rocket" &&
                rule.Attribute("gainItemIdx")?.Value == "magnet" &&
                rule.Attribute("probability")?.Value == "33" &&
                rule.Attribute("sourceId")?.Value == "7" &&
                rule.Attribute("targetId")?.Value == "5");
        bool hasUnpackedExecutable = File.Exists(Path.Combine(
            Path.GetFullPath(rhoCatalogTestRoot),
            "KartRiderU.exe"));
        int totalAbilityCount = 0;
        int resolvedAbilityCount = 0;
        bool hasValidAbilityCounts = int.TryParse(
                abilityRoot?.Attribute("total")?.Value,
                out totalAbilityCount) &&
            int.TryParse(
                abilityRoot?.Attribute("resolved")?.Value,
                out resolvedAbilityCount) &&
            totalAbilityCount == 721 &&
            resolvedAbilityCount >= 719 &&
            resolvedAbilityCount <= totalAbilityCount;
        int exportedInventoryCount = inventoryRoot?.Elements("Item").Count() ?? 0;
        int exportedInventoryCategories = inventoryRoot?
            .Elements("Item")
            .Select(item => item.Attribute("category")?.Value)
            .Distinct()
            .Count() ?? 0;
        int exportedKartInventoryCount = inventoryRoot?
            .Elements("Item")
            .Count(item => item.Attribute("category")?.Value == "3") ?? 0;
        if (exportedCatalog.Root?.Attribute("formatVersion")?.Value != "3" ||
            exportedCatalog.Root?.Attribute("protocolVersion")?.Value != "5136" ||
            exportedCatalog.Root?.Attribute("region")?.Value != "kr" ||
            exportedCatalog.Root?.Attribute("sourceAaaSha256")?.Value.Length != 64 ||
            (hasUnpackedExecutable &&
                exportedCatalog.Root?.Attribute("sourceExecutableSha256")?.Value.Length != 64) ||
            itemSymbolsRoot?.Attribute("resolution")?.Value != "verified-partial" ||
            abilityRoot?.Attribute("numericResolution")?.Value != "verified-partial" ||
            !HasItemSymbol("goldEggMine", "83") ||
            !HasItemSymbol("superMagnet", "103") ||
            !HasItemSymbol("siren", "24") ||
            !hasAllHardcodedExecutableSymbols ||
            goldenChickenBananaRule == null ||
            goldenChickenMagnetRule == null ||
            redLotusMagnetRule == null ||
            redLotusRocketRule == null ||
            exportedInventoryCount < 6800 ||
            exportedInventoryCategories < 60 ||
            exportedKartInventoryCount < 1200 ||
            inventoryRoot?.Elements("Item").Any(item =>
                item.Attribute("id")?.Value == "0") != false ||
            !hasValidAbilityCounts)
        {
            Console.Error.WriteLine(
                "P5136 verified-partial kart ability catalog validation failed: " +
                $"format={exportedCatalog.Root?.Attribute("formatVersion")?.Value}; " +
                $"symbols={itemSymbolsRoot?.Attribute("resolution")?.Value}; " +
                $"abilities={abilityRoot?.Attribute("numericResolution")?.Value}; " +
                $"goldEgg={HasItemSymbol("goldEggMine", "83")}; " +
                $"superMagnet={HasItemSymbol("superMagnet", "103")}; " +
                $"siren={HasItemSymbol("siren", "24")}; " +
                $"hardcodedExecutableSymbols={hasAllHardcodedExecutableSymbols}; " +
                $"rules={goldenChickenBananaRule != null}/" +
                $"{goldenChickenMagnetRule != null}/{redLotusMagnetRule != null}/" +
                $"{redLotusRocketRule != null}; inventory={exportedInventoryCount}/" +
                $"{exportedInventoryCategories}/{exportedKartInventoryCount}; " +
                $"counts={hasValidAbilityCounts} " +
                $"({resolvedAbilityCount}/{totalAbilityCount})");
            return 1;
        }

        Kart.kartName = new Dictionary<int, string>();
        Kart.kartSpec = new Dictionary<string, System.Xml.XmlDocument>();
        if (!KartRhoFile.TryLoadKartCatalogXml(
                catalogPath,
                out int kartNameCount,
                out int kartSpecCount,
                out string loadError) ||
            File.Exists(catalogPath + ".new") ||
            kartNameCount != exportedNameCount ||
            kartSpecCount != exportedSpecCount ||
            kartNameCount < 100 ||
            kartSpecCount < 100 ||
            !Kart.kartName.TryGetValue(1453, out string goldenChickenName) ||
            !Kart.kartSpec.TryGetValue(goldenChickenName, out var goldenChickenSpec) ||
            !Kart.kartName.TryGetValue(1450, out string redLotusName) ||
            !Kart.kartSpec.TryGetValue(redLotusName, out var redLotusSpec) ||
            goldenChickenSpec.GetElementsByTagName("BodyParam").Count == 0 ||
            goldenChickenSpec.GetElementsByTagName("BodyParam")[0] is not System.Xml.XmlElement goldenChickenBody ||
            goldenChickenBody.GetAttribute("ItemSlotCapacity") != "3" ||
            goldenChickenBody.GetAttribute("SpecialSlotCapacity") != "2" ||
            redLotusSpec.GetElementsByTagName("BodyParam").Count == 0 ||
            redLotusSpec.GetElementsByTagName("BodyParam")[0] is not System.Xml.XmlElement redLotusBody ||
            redLotusBody.GetAttribute("ItemSlotCapacity") != "3" ||
            redLotusBody.GetAttribute("SpecialSlotCapacity") != "2" ||
            KartCatalogInventory.TotalItemCount != exportedInventoryCount ||
            KartCatalogInventory.CategoryCount != exportedInventoryCategories ||
            KartCatalogAbilities.ResolvedRuleCount == 0)
        {
            Console.Error.WriteLine(
                $"P5136 kart catalog XML load failed: names={kartNameCount}, " +
                $"specs={kartSpecCount}, error={loadError}");
            return 1;
        }

        string actualGrantProfile = Path.Combine(
            Path.GetTempPath(),
            $"P5136-CleanInventory-{Environment.ProcessId}-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(actualGrantProfile);
            var actualGrant = Korean5136Inventory.BuildGrantSnapshotForTesting(
                actualGrantProfile,
                slotChanger: 999,
                preventItem: false);
            IReadOnlyList<int> actualPacketSizes =
                Korean5136Inventory.BuildItemPacketSizesForTesting(
                    actualGrantProfile,
                    slotChanger: 999,
                    preventItem: false);
            ushort[] unsafeCharacterIds =
            {
                45, 47, 48, 52, 59, 116, 117, 124, 128, 130, 137, 144,
                147, 149, 159, 175, 176, 184, 192, 193, 194, 195, 196,
                197, 231, 245, 246, 247, 265, 301, 302, 333, 350, 376,
                377, 391, 392, 396, 397
            };
            var actualParts = actualGrant.Where(item =>
                item.Category is >= 63 and <= 66).ToArray();
            if (actualGrant.Count != 5635 ||
                actualGrant.Count(item => item.Category == 1) != 342 ||
                actualGrant.Count(item => item.Category == 3) != 1296 ||
                actualGrant[0].Category != 21 ||
                actualGrant[^1].Category != 3 ||
                actualGrant.Any(item => item.Id == 0) ||
                actualGrant.Any(item =>
                    item.Category == 1 && unsafeCharacterIds.Contains(item.Id)) ||
                actualPacketSizes.Any(size => size <= 0 || size > 100) ||
                actualParts.Length != 320 ||
                actualParts.Any(item => item.PartFlag != 1 || item.Grade is < 1 or > 4) ||
                actualGrant.Any(item =>
                    (item.Category < 63 || item.Category > 66) && item.PartFlag != 0) ||
                File.Exists(Path.Combine(actualGrantProfile, "Item.xml")) ||
                File.Exists(Path.Combine(actualGrantProfile, "NewKart.xml")))
            {
                Console.Error.WriteLine(
                    $"P5136 extracted all-items grant validation failed: " +
                    $"items={actualGrant.Count}, " +
                    $"karts={actualGrant.Count(item => item.Category == 3)}");
                return 1;
            }
        }
        finally
        {
            if (Directory.Exists(actualGrantProfile))
            {
                Directory.Delete(actualGrantProfile, recursive: true);
            }
        }

        int ReadFiringStep(XElement rule) => int.TryParse(
            rule.Attribute("firingStep")?.Value,
            out int step)
                ? step
                : 0;
        if (!KartCatalogAbilities.TryGetTransform(
                1453,
                8,
                goldenChickenBananaRule.Attribute("gitType")?.Value ?? string.Empty,
                out KartCatalogAbilityRule runtimeGoldenBanana) ||
            runtimeGoldenBanana.TargetItemId != 83 ||
            !KartCatalogAbilities.TryGetTransform(
                1453,
                5,
                goldenChickenMagnetRule.Attribute("gitType")?.Value ?? string.Empty,
                out KartCatalogAbilityRule runtimeGoldenMagnet) ||
            runtimeGoldenMagnet.TargetItemId != 103 ||
            !KartCatalogAbilities.HasTransformDefinition(1453, 8) ||
            KartCatalogAbilities.TryGetTransform(
                1453,
                8,
                "FlagIndi",
                out _) ||
            !KartCatalogAbilities.TryGetFiringToGain(
                1450,
                5,
                ReadFiringStep(redLotusMagnetRule),
                redLotusMagnetRule.Attribute("gameType")?.Value ?? string.Empty,
                out KartCatalogAbilityRule runtimeRedLotusMagnet) ||
            runtimeRedLotusMagnet.TargetItemId != 24 ||
            runtimeRedLotusMagnet.Probability != 100 ||
            !KartCatalogAbilities.TryGetFiringToGain(
                1450,
                7,
                ReadFiringStep(redLotusRocketRule),
                redLotusRocketRule.Attribute("gameType")?.Value ?? string.Empty,
                out KartCatalogAbilityRule runtimeRedLotusRocket) ||
            runtimeRedLotusRocket.TargetItemId != 5 ||
            runtimeRedLotusRocket.Probability != 33)
        {
            Console.Error.WriteLine("P5136 loaded kart ability runtime lookup failed.");
            return 1;
        }

        string brokenCatalogPath = catalogPath + ".broken";
        Dictionary<int, string> namesBeforeBrokenLoad = Kart.kartName;
        Dictionary<string, System.Xml.XmlDocument> specsBeforeBrokenLoad = Kart.kartSpec;
        IReadOnlyList<KartCatalogInventoryItem> inventoryBeforeBrokenLoad =
            KartCatalogInventory.GetItemsSnapshot();
        int abilitiesBeforeBrokenLoad = KartCatalogAbilities.TotalRuleCount;
        try
        {
            File.WriteAllText(
                brokenCatalogPath,
                "<KartCatalog formatVersion=\"3\"><Names>",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (KartRhoFile.TryLoadKartCatalogXml(
                    brokenCatalogPath,
                    out _,
                    out _,
                    out _) ||
                !ReferenceEquals(namesBeforeBrokenLoad, Kart.kartName) ||
                !ReferenceEquals(specsBeforeBrokenLoad, Kart.kartSpec) ||
                !ReferenceEquals(
                    inventoryBeforeBrokenLoad,
                    KartCatalogInventory.GetItemsSnapshot()) ||
                KartCatalogAbilities.TotalRuleCount != abilitiesBeforeBrokenLoad)
            {
                Console.Error.WriteLine(
                    "P5136 broken catalog replaced an already published runtime catalog.");
                return 1;
            }
        }
        finally
        {
            if (File.Exists(brokenCatalogPath))
            {
                File.Delete(brokenCatalogPath);
            }
        }

        Console.WriteLine(
            $"P5136 read-only RHO/XML kart catalog passed: names={kartNameCount}, " +
            $"specs={kartSpecCount}, inventory={KartCatalogInventory.TotalItemCount}, " +
            $"kart1453={goldenChickenName}, kart1450={redLotusName}");
    }
    finally
    {
        if (!keepCatalogOutput && File.Exists(catalogPath))
        {
            File.Delete(catalogPath);
        }
    }
}

if (string.IsNullOrWhiteSpace(rhoCatalogTestRoot))
{
    string syntheticRoot = Path.Combine(
        Path.GetTempPath(),
        $"P5136-SyntheticCatalog-{Environment.ProcessId}-{Guid.NewGuid():N}");
    string syntheticProfile = Path.Combine(syntheticRoot, "Profile");
    string syntheticData = Path.Combine(syntheticRoot, "Data");
    string syntheticCatalogPath = Path.Combine(syntheticProfile, "KartCatalog.xml");
    string syntheticAaaPath = Path.Combine(syntheticData, "aaa.pk");
    try
    {
        Directory.CreateDirectory(syntheticProfile);
        Directory.CreateDirectory(syntheticData);
        byte[] syntheticAaa = Encoding.ASCII.GetBytes("synthetic-p5136-aaa");
        File.WriteAllBytes(syntheticAaaPath, syntheticAaa);
        string syntheticAaaHash = Convert.ToHexString(
            SHA256.HashData(syntheticAaa)).ToLowerInvariant();

        string SyntheticKartName(int id) => id switch
        {
            1450 => "shurikenV1",
            1453 => "chicken_goldV1",
            _ => $"syntheticKart{id}"
        };
        XElement namesElement = new XElement(
            "Names",
            Enumerable.Range(1, 1456).Select(id => new XElement(
                "Kart",
                new XAttribute("id", id),
                new XAttribute("name", SyntheticKartName(id)))));
        XElement specsElement = new XElement(
            "Specs",
            Enumerable.Range(1, 1300)
                .Concat(new[] { 1450, 1453 })
                .Select(id => new XElement(
                    "Spec",
                    new XAttribute("name", SyntheticKartName(id)),
                    new XElement(
                        "BodyParam",
                        id is 1450 or 1453
                            ? new object[]
                            {
                                new XAttribute("ItemSlotCapacity", "3"),
                                new XAttribute("SpecialSlotCapacity", "2")
                            }
                            : Array.Empty<object>()))));
        XElement syntheticInventoryElement = new XElement(
            "Inventory",
            Enumerable.Range(1, 70)
                .Where(category => category != 3)
                .SelectMany(category => Enumerable.Range(1, 100).Select(id => new XElement(
                    "Item",
                    new XAttribute("category", category),
                    new XAttribute("id", id),
                    new XAttribute("name", $"syntheticItem{category}_{id}")))),
            Enumerable.Range(1, 1296)
                .Concat(new[] { 1450, 1453 })
                .Select(id => new XElement(
                    "Item",
                    new XAttribute("category", 3),
                    new XAttribute("id", id),
                    new XAttribute("name", SyntheticKartName(id)))));
        syntheticInventoryElement.SetAttributeValue(
            "total",
            syntheticInventoryElement.Elements("Item").Count());
        syntheticInventoryElement.SetAttributeValue(
            "categories",
            syntheticInventoryElement
                .Elements("Item")
                .Select(item => item.Attribute("category")?.Value)
                .Distinct()
                .Count());
        XElement itemSymbolsElement = new XElement(
            "ItemSymbols",
            new XAttribute("resolution", "synthetic-verified"),
            new XAttribute("total", 36),
            new XElement("Item", new XAttribute("name", "goldEggMine"), new XAttribute("id", 83), new XAttribute("evidence", "synthetic")),
            new XElement("Item", new XAttribute("name", "superMagnet"), new XAttribute("id", 103), new XAttribute("evidence", "synthetic")),
            new XElement("Item", new XAttribute("name", "siren"), new XAttribute("id", 24), new XAttribute("evidence", "synthetic")),
            Enumerable.Range(0, 33).Select(id => new XElement(
                "Item",
                new XAttribute("name", $"syntheticItem{id}"),
                new XAttribute("id", 200 + id),
                new XAttribute("evidence", "synthetic"))));
        XElement transformAbilities = new XElement(
            "TransformByKart",
            new XElement(
                "Rule",
                new XAttribute("kartId", 1453),
                new XAttribute("srcIdx", "banana"),
                new XAttribute("dstIdx", "goldEggMine"),
                new XAttribute("gitType", "no_flag"),
                new XAttribute("probability", 100),
                new XAttribute("sourceId", 8),
                new XAttribute("targetId", 83)),
            new XElement(
                "Rule",
                new XAttribute("kartId", 1453),
                new XAttribute("srcIdx", "magnet"),
                new XAttribute("dstIdx", "superMagnet"),
                new XAttribute("gitType", "no_flag"),
                new XAttribute("probability", 100),
                new XAttribute("sourceId", 5),
                new XAttribute("targetId", 103)),
            Enumerable.Range(0, 696).Select(index => new XElement(
                "Rule",
                new XAttribute("kartId", 1 + index % 100),
                new XAttribute("srcIdx", "banana"),
                new XAttribute("dstIdx", "goldEggMine"),
                new XAttribute("gitType", "no_flag"),
                new XAttribute("probability", 100),
                new XAttribute("sourceId", 8),
                new XAttribute("targetId", 83))));
        XElement firingAbilities = new XElement(
            "FiringToGain",
            new XElement(
                "Rule",
                new XAttribute("kartId", 1450),
                new XAttribute("firingItemIdx", "magnet"),
                new XAttribute("firingStep", 1),
                new XAttribute("gainItemIdx", "siren"),
                new XAttribute("probability", 100),
                new XAttribute("sourceId", 5),
                new XAttribute("targetId", 24)),
            new XElement(
                "Rule",
                new XAttribute("kartId", 1450),
                new XAttribute("firingItemIdx", "rocket"),
                new XAttribute("firingStep", 1),
                new XAttribute("gainItemIdx", "magnet"),
                new XAttribute("probability", 33),
                new XAttribute("sourceId", 7),
                new XAttribute("targetId", 5)));
        XDocument syntheticCatalog = new XDocument(
            new XElement(
                "KartCatalog",
                new XAttribute("formatVersion", "3"),
                new XAttribute("protocolVersion", "5136"),
                new XAttribute("region", "kr"),
                new XAttribute("sourceAaaSha256", syntheticAaaHash),
                namesElement,
                specsElement,
                syntheticInventoryElement,
                itemSymbolsElement,
                new XElement(
                    "Abilities",
                    new XAttribute("numericResolution", "synthetic-verified"),
                    new XAttribute("total", 700),
                    new XAttribute("resolved", 700),
                    transformAbilities,
                    firingAbilities,
                    new XElement("FiredToGain"))));
        syntheticCatalog.Save(syntheticCatalogPath);

        if (!KartRhoFile.TryLoadKartCatalogXml(
                syntheticCatalogPath,
                out int syntheticNames,
                out int syntheticSpecs,
                out string syntheticError) ||
            syntheticNames != 1456 ||
            syntheticSpecs != 1302 ||
            KartCatalogInventory.TotalItemCount !=
                syntheticInventoryElement.Elements("Item").Count() ||
            KartCatalogAbilities.TotalRuleCount != 700)
        {
            Console.Error.WriteLine(
                $"P5136 synthetic catalog load failed: {syntheticError}");
            return 1;
        }

        string cleanInventoryProfile = Path.Combine(syntheticRoot, "CleanInventoryProfile");
        Directory.CreateDirectory(cleanInventoryProfile);
        var firstGrant = Korean5136Inventory.BuildGrantSnapshotForTesting(
            cleanInventoryProfile,
            slotChanger: 999,
            preventItem: false);
        var secondGrant = Korean5136Inventory.BuildGrantSnapshotForTesting(
            cleanInventoryProfile,
            slotChanger: 999,
            preventItem: false);
        IReadOnlyList<int> syntheticPacketSizes =
            Korean5136Inventory.BuildItemPacketSizesForTesting(
                cleanInventoryProfile,
                slotChanger: 999,
                preventItem: false);
        int grantedKarts = firstGrant.Count(item => item.Category == 3);
        var syntheticParts = firstGrant.Where(item =>
            item.Category is >= 63 and <= 66).ToArray();
        if (File.Exists(Path.Combine(cleanInventoryProfile, "Item.xml")) ||
            File.Exists(Path.Combine(cleanInventoryProfile, "NewKart.xml")) ||
            firstGrant.Count < 5500 ||
            grantedKarts != 1298 ||
            firstGrant.Count(item => item.Category == 1) != 95 ||
            firstGrant.Any(item =>
                item.Category == 1 &&
                new ushort[] { 45, 47, 48, 52, 59 }.Contains(item.Id)) ||
            firstGrant[0].Category != 21 ||
            firstGrant[^1].Category != 3 ||
            !firstGrant.Any(item => item.Category == 3 && item.Id == 1450) ||
            !firstGrant.Any(item => item.Category == 3 && item.Id == 1453) ||
            firstGrant.Any(item => item.Id == 0) ||
            syntheticPacketSizes.Any(size => size <= 0 || size > 100) ||
            syntheticParts.Length != 320 ||
            syntheticParts.Any(item => item.PartFlag != 1 || item.Grade is < 1 or > 4) ||
            firstGrant.First(item => item.Category == 1 && item.Id == 1).Amount != 1 ||
            firstGrant.First(item => item.Category == 7 && item.Id == 3).Amount != 1 ||
            firstGrant.First(item => item.Category == 7 && item.Id == 1).Amount != 999 ||
            firstGrant.First(item => item.Category == 9 && item.Id == 1).Amount != 999 ||
            !firstGrant.SequenceEqual(secondGrant))
        {
            Console.Error.WriteLine(
                $"P5136 clean-profile all-items grant failed: " +
                $"items={firstGrant.Count}, karts={grantedKarts}");
            return 1;
        }

        Dictionary<int, string> publishedNames = Kart.kartName;
        Dictionary<string, System.Xml.XmlDocument> publishedSpecs = Kart.kartSpec;
        IReadOnlyList<KartCatalogInventoryItem> publishedInventory =
            KartCatalogInventory.GetItemsSnapshot();
        int publishedAbilities = KartCatalogAbilities.TotalRuleCount;
        XDocument wrongProtocol = new XDocument(syntheticCatalog);
        wrongProtocol.Root!.SetAttributeValue("protocolVersion", "9999");
        string wrongProtocolPath = Path.Combine(syntheticProfile, "WrongProtocol.xml");
        wrongProtocol.Save(wrongProtocolPath);
        if (KartRhoFile.TryLoadKartCatalogXml(
                wrongProtocolPath,
                out _,
                out _,
                out _) ||
            !ReferenceEquals(publishedNames, Kart.kartName) ||
            !ReferenceEquals(publishedSpecs, Kart.kartSpec) ||
            !ReferenceEquals(publishedInventory, KartCatalogInventory.GetItemsSnapshot()) ||
            KartCatalogAbilities.TotalRuleCount != publishedAbilities)
        {
            Console.Error.WriteLine("P5136 wrong-protocol catalog was accepted.");
            return 1;
        }

        XDocument incompleteCatalog = new XDocument(syntheticCatalog);
        incompleteCatalog.Root!
            .Element("Names")!
            .Elements("Kart")
            .Skip(10)
            .Remove();
        string incompleteCatalogPath = Path.Combine(syntheticProfile, "Incomplete.xml");
        incompleteCatalog.Save(incompleteCatalogPath);
        if (KartRhoFile.TryLoadKartCatalogXml(
                incompleteCatalogPath,
                out _,
                out _,
                out _) ||
            !ReferenceEquals(publishedNames, Kart.kartName) ||
            !ReferenceEquals(publishedSpecs, Kart.kartSpec) ||
            !ReferenceEquals(publishedInventory, KartCatalogInventory.GetItemsSnapshot()) ||
            KartCatalogAbilities.TotalRuleCount != publishedAbilities)
        {
            Console.Error.WriteLine("P5136 incomplete catalog was accepted.");
            return 1;
        }

        XDocument incompleteInventoryCatalog = new XDocument(syntheticCatalog);
        incompleteInventoryCatalog.Root!
            .Element("Inventory")!
            .Elements("Item")
            .Skip(1000)
            .Remove();
        incompleteInventoryCatalog.Root!
            .Element("Inventory")!
            .SetAttributeValue("total", 1000);
        string incompleteInventoryPath = Path.Combine(
            syntheticProfile,
            "IncompleteInventory.xml");
        incompleteInventoryCatalog.Save(incompleteInventoryPath);
        if (KartRhoFile.TryLoadKartCatalogXml(
                incompleteInventoryPath,
                out _,
                out _,
                out _) ||
            !ReferenceEquals(publishedNames, Kart.kartName) ||
            !ReferenceEquals(publishedSpecs, Kart.kartSpec) ||
            !ReferenceEquals(publishedInventory, KartCatalogInventory.GetItemsSnapshot()) ||
            KartCatalogAbilities.TotalRuleCount != publishedAbilities)
        {
            Console.Error.WriteLine("P5136 incomplete inventory catalog was accepted.");
            return 1;
        }

        File.WriteAllBytes(syntheticAaaPath, Encoding.ASCII.GetBytes("mismatched-aaa"));
        if (KartRhoFile.TryLoadKartCatalogXml(
                syntheticCatalogPath,
                out _,
                out _,
                out _) ||
            !ReferenceEquals(publishedNames, Kart.kartName) ||
            !ReferenceEquals(publishedSpecs, Kart.kartSpec) ||
            !ReferenceEquals(publishedInventory, KartCatalogInventory.GetItemsSnapshot()) ||
            KartCatalogAbilities.TotalRuleCount != publishedAbilities)
        {
            Console.Error.WriteLine("P5136 mismatched catalog fingerprint was accepted.");
            return 1;
        }

        Console.WriteLine(
            "P5136 synthetic catalog schema/completeness/fingerprint smoke test passed.");
    }
    finally
    {
        if (Directory.Exists(syntheticRoot))
        {
            Directory.Delete(syntheticRoot, recursive: true);
        }
    }
}

bool ApplyPlant(
    short category,
    short id,
    Korean5136PlantGameMode mode,
    out ExcSpecs specs)
{
    specs = new ExcSpecs();
    return Korean5136PlantPerformance.Apply(specs, category, id, mode);
}

bool plantSnapshotValid =
    Korean5136PlantPerformance.EntryCount == 91 &&
    Korean5136PlantPerformance.GetCategoryEntryCount(43) == 23 &&
    Korean5136PlantPerformance.GetCategoryEntryCount(44) == 15 &&
    Korean5136PlantPerformance.GetCategoryEntryCount(45) == 23 &&
    Korean5136PlantPerformance.GetCategoryEntryCount(46) == 30 &&
    ApplyPlant(43, 1, Korean5136PlantGameMode.Speed, out ExcSpecs engine1) &&
    engine1.Plant43_TransAccelFactor == 0.002f &&
    engine1.Plant43_DragFactor == -0.0007f &&
    ApplyPlant(43, 5, Korean5136PlantGameMode.Item, out ExcSpecs engine5) &&
    engine5.Plant43_StartForwardAccelSpeed == 0f &&
    engine5.Plant43_StartForwardAccelItem == 0.04f &&
    ApplyPlant(43, 6, Korean5136PlantGameMode.Item, out ExcSpecs engine6Item) &&
    engine6Item.Plant43_DragFactor == 0f &&
    ApplyPlant(43, 6, Korean5136PlantGameMode.Speed, out ExcSpecs engine6Speed) &&
    engine6Speed.Plant43_DragFactor == -0.0021f &&
    ApplyPlant(45, 14, Korean5136PlantGameMode.Speed, out ExcSpecs wheel14Speed) &&
    wheel14Speed.Plant45_DriftEscapeForce == 0f &&
    ApplyPlant(45, 14, Korean5136PlantGameMode.Item, out ExcSpecs wheel14Item) &&
    wheel14Item.Plant45_DriftEscapeForce == 50f &&
    wheel14Item.Plant45_DriftMaxGauge == 40f &&
    ApplyPlant(46, 6, Korean5136PlantGameMode.Speed, out ExcSpecs kit6) &&
    kit6.Plant46_NormalBoosterTime == 60f &&
    kit6.Plant46_AnimalBoosterTime == 80f &&
    ApplyPlant(46, 9, Korean5136PlantGameMode.Speed, out ExcSpecs kit9) &&
    kit9.Plant46_StartBoosterTimeSpeed == 195f &&
    ApplyPlant(46, 16, Korean5136PlantGameMode.Item, out ExcSpecs kit16) &&
    kit16.Plant46_GripBrake == 0f &&
    kit16.Plant46_SlipBrake == 10f &&
    ApplyPlant(46, 17, Korean5136PlantGameMode.Item, out ExcSpecs kit17Item) &&
    kit17Item.Plant46_AnimalBoosterTime == 0f &&
    ApplyPlant(46, 17, Korean5136PlantGameMode.Battle, out ExcSpecs kit17Battle) &&
    kit17Battle.Plant46_AnimalBoosterTime == 100f &&
    kit17Battle.Plant46_SlipBrake == 9f &&
    ApplyPlant(46, 21, Korean5136PlantGameMode.Speed, out ExcSpecs kit21) &&
    kit21.Plant46_StartBoosterTimeSpeed == 150f &&
    ApplyPlant(46, 22, Korean5136PlantGameMode.Speed, out ExcSpecs kit22) &&
    kit22.Plant46_ForwardAccel == 1.5f &&
    ApplyPlant(46, 11, Korean5136PlantGameMode.Item, out ExcSpecs kit11Item) &&
    kit11Item.Plant46_ItemSlotCapacity == 0 &&
    ApplyPlant(46, 11, Korean5136PlantGameMode.Battle, out ExcSpecs kit11Battle) &&
    kit11Battle.Plant46_ItemSlotCapacity == 3 &&
    ApplyPlant(46, 12, Korean5136PlantGameMode.TimeAttack, out ExcSpecs kit12) &&
    kit12.Plant46_SpeedSlotCapacity == 3 &&
    Korean5136PlantPerformance.FromRoomGameType(1) == Korean5136PlantGameMode.Speed &&
    Korean5136PlantPerformance.FromRoomGameType(4) == Korean5136PlantGameMode.Item &&
    !ApplyPlant(46, 31, Korean5136PlantGameMode.Speed, out _);
if (!plantSnapshotValid)
{
    Console.Error.WriteLine("P5136 client plant-part performance snapshot validation failed.");
    return 1;
}
Console.WriteLine("P5136 client plant-part performance snapshot passed (91/91 entries).");

string itemProbabilityTestRoot = Environment.GetEnvironmentVariable("P5136_ITEM_TEST_ROOT");
if (!string.IsNullOrWhiteSpace(itemProbabilityTestRoot))
{
    ItemProbabilityConfiguration clientItemDefaults =
        ItemProbabilityService.LoadClientDefaults(
            itemProbabilityTestRoot,
            out string itemProbabilitySource);
    long individualWeight = clientItemDefaults.Individual.Sum(
        entry => entry.GetWeight(ItemProbabilityRankBand.Combined));
    long teamWeight = clientItemDefaults.Team.Sum(
        entry => entry.GetWeight(ItemProbabilityRankBand.Combined));
    if (clientItemDefaults.Individual.Count != 14 ||
        clientItemDefaults.Team.Count != 18 ||
        individualWeight <= 0 ||
        teamWeight <= 0)
    {
        Console.Error.WriteLine(
            $"P5136 item.rho probability table loading failed: " +
            $"individual={clientItemDefaults.Individual.Count}/{individualWeight}, " +
            $"team={clientItemDefaults.Team.Count}/{teamWeight}, source={itemProbabilitySource}");
        return 1;
    }
    Console.WriteLine(
        $"P5136 item.rho probability tables loaded: individual=14/{individualWeight}, " +
        $"team=18/{teamWeight}, source={itemProbabilitySource}");
}

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

if (!RunRoomNameSpeedKeywordSmokeTests(out string roomKeywordFailure))
{
    Console.Error.WriteLine($"P5136 room-name speed keyword parsing failed: {roomKeywordFailure}");
    return 1;
}

Console.WriteLine("P5136 ASCII room-name speed keyword smoke test passed.");

using (OutPacket loginProfile = new OutPacket())
{
    loginProfile.WriteUInt(0x8B019610);
    loginProfile.WriteUInt(0xBA06B093);
    loginProfile.WriteUInt(Adler32Helper.GenerateAdler32_ASCII("AccountDataProfile", 0));
    loginProfile.WriteByte(0);

    BmlObject root = new BmlObject { Name = "profile" };
    root.SubObjects.Add(Tuple.Create(
        "username",
        new BmlObject { Name = "username", Value = "Yany2" }));
    root.Save(loginProfile);

    using InPacket loginPacket = new InPacket(loginProfile.ToArray());
    if (Korean5136LoginProfileReader.ReadUsername(loginPacket) != "Yany2")
    {
        Console.Error.WriteLine("P5136 AccountDataProfile username parsing failed.");
        return 1;
    }
}

string identityValidationRoot = Path.Combine(Path.GetTempPath(), "KartRider-P5136-identity-root");
if (!ClientIdentityValidator.TryNormalize(
        "라이더2",
        identityValidationRoot,
        out string normalizedIdentity,
        out _) ||
    normalizedIdentity != "라이더2" ||
    ClientIdentityValidator.TryNormalize(
        "..\\escape",
        identityValidationRoot,
        out _,
        out _))
{
    Console.Error.WriteLine("P5136 account identity validation failed.");
    return 1;
}

Console.WriteLine("P5136 login identity parser/validator smoke test passed.");

if (!RunPacketTraceSmokeTests(out string packetTraceFailure))
{
    Console.Error.WriteLine($"P5136 asynchronous packet trace failed: {packetTraceFailure}");
    return 1;
}

Console.WriteLine("P5136 asynchronous packet trace/UI summary smoke test passed.");

ClientPortTopology topology = ClientBuildProfiles.Korean5136.Ports;
ushort defaultLoginPort = topology.ResolveLoginTcpPort(topology.DefaultConfiguredPort);
if (topology.ResolveConfiguredPortFromLoginTcp(defaultLoginPort) != topology.DefaultConfiguredPort)
{
    Console.Error.WriteLine("P5136 login/configured port conversion failed.");
    return 1;
}
if (topology.MaximumLoginTcpPort != 65534)
{
    Console.Error.WriteLine("P5136 maximum login TCP port calculation failed.");
    return 1;
}
try
{
    topology.ResolveConfiguredPortFromLoginTcp(1);
    Console.Error.WriteLine("P5136 invalid login TCP lower bound was accepted.");
    return 1;
}
catch (InvalidOperationException)
{
}

ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean5136);

RaceSettlementPacketKind[] p5136SettlementOrder =
    MultyPlayer.GetSettlementPacketOrder(ClientBuild.Korean5136);
RaceSettlementPacketKind[] modernSettlementOrder =
    MultyPlayer.GetSettlementPacketOrder(ClientBuild.Modern);
if (!p5136SettlementOrder.SequenceEqual(new[]
    {
        RaceSettlementPacketKind.GameNextStage,
        RaceSettlementPacketKind.GameResult,
        RaceSettlementPacketKind.GameControl
    }) ||
    !modernSettlementOrder.SequenceEqual(new[]
    {
        RaceSettlementPacketKind.GameControl,
        RaceSettlementPacketKind.GameNextStage,
        RaceSettlementPacketKind.GameResult
    }))
{
    Console.Error.WriteLine("P5136 ceremony settlement packet ordering failed.");
    return 1;
}

Console.WriteLine("P5136 ceremony result-before-control ordering smoke test passed.");

if (!RunUdpEndpointBindingSmokeTests(out string udpBindingFailure))
{
    Console.Error.WriteLine($"P5136 UDP generation first-bind failed: {udpBindingFailure}");
    return 1;
}

Console.WriteLine("P5136 UDP/P2P generation first-bind smoke test passed.");

if (!RunItemPickupContextSmokeTests(out string itemPickupFailure))
{
    Console.Error.WriteLine($"P5136 item pickup rank/position parsing failed: {itemPickupFailure}");
    return 1;
}

Console.WriteLine("P5136 item pickup rank/position parsing smoke test passed.");

if (!RunBarricadePlacementSmokeTests(out string barricadeFailure))
{
    Console.Error.WriteLine($"P5136 barricade placement routing failed: {barricadeFailure}");
    return 1;
}

Console.WriteLine("P5136 sender-inclusive barricade placement smoke test passed.");

if (!Korean5136Protocol.TryResolveRoomGameType(67, out byte individualCombineType) ||
    individualCombineType != 1 ||
    !Korean5136Protocol.TryResolveRoomGameType(23, out byte individualInfiniteType) ||
    individualInfiniteType != 1 ||
    !Korean5136Protocol.TryResolveRoomGameType(68, out byte teamCombineType) ||
    teamCombineType != 3 ||
    !Korean5136Protocol.TryResolveRoomGameType(24, out byte teamInfiniteType) ||
    teamInfiniteType != 3)
{
    Console.Error.WriteLine("P5136 channel-to-room game type mapping failed.");
    return 1;
}

List<int> channelRoomIds = new List<int>();
try
{
    // Create more than one page of Infinite rooms first. A Combine room that
    // was created later must still appear on page zero after filtering.
    for (int index = 0; index < 11; index++)
    {
        int roomId = RoomManager.CreateRoom();
        channelRoomIds.Add(roomId);
        GameRoom room = RoomManager.GetRoom(roomId);
        room.GameType = 1;
        room.P5136ChannelGameType = 23;
        room.P5136ChannelId = 13;
    }

    int combineRoomA = RoomManager.CreateRoom();
    int combineRoomB = RoomManager.CreateRoom();
    int teamCombineRoom = RoomManager.CreateRoom();
    int teamInfiniteRoom = RoomManager.CreateRoom();
    channelRoomIds.AddRange(new[]
    {
        combineRoomA,
        combineRoomB,
        teamCombineRoom,
        teamInfiniteRoom
    });

    RoomManager.GetRoom(combineRoomA).GameType = 1;
    RoomManager.GetRoom(combineRoomA).P5136ChannelGameType = 67;
    RoomManager.GetRoom(combineRoomA).P5136ChannelId = 11;
    RoomManager.GetRoom(combineRoomB).GameType = 1;
    RoomManager.GetRoom(combineRoomB).P5136ChannelGameType = 67;
    RoomManager.GetRoom(combineRoomB).P5136ChannelId = 12;
    RoomManager.GetRoom(teamCombineRoom).GameType = 3;
    RoomManager.GetRoom(teamCombineRoom).P5136ChannelGameType = 68;
    RoomManager.GetRoom(teamCombineRoom).P5136ChannelId = 17;
    RoomManager.GetRoom(teamInfiniteRoom).GameType = 3;
    RoomManager.GetRoom(teamInfiniteRoom).P5136ChannelGameType = 24;
    RoomManager.GetRoom(teamInfiniteRoom).P5136ChannelId = 19;

    // Concrete A/B records are currently aggregated within one mode family;
    // captures prove family isolation but not separate A/B room shards.
    Dictionary<int, GameRoom> combineRooms = RoomManager.GetRoomsByPage(
        0,
        room => room.P5136ChannelGameType == 67,
        out int combineRoomCount);
    if (combineRoomCount != 2 ||
        !combineRooms.ContainsKey(combineRoomA) ||
        !combineRooms.ContainsKey(combineRoomB) ||
        combineRooms.Values.Any(room => room.P5136ChannelGameType != 67))
    {
        Console.Error.WriteLine("P5136 channel-family room filtering failed.");
        return 1;
    }

    Dictionary<int, GameRoom> teamCombineRooms = RoomManager.GetRoomsByPage(
        0,
        room => room.P5136ChannelGameType == 68,
        out int teamCombineRoomCount);
    if (teamCombineRoomCount != 1 ||
        !teamCombineRooms.ContainsKey(teamCombineRoom) ||
        teamCombineRooms.ContainsKey(teamInfiniteRoom))
    {
        Console.Error.WriteLine("P5136 team Combine/Infinite room isolation failed.");
        return 1;
    }
}
finally
{
    foreach (int roomId in channelRoomIds)
        RoomManager._rooms.TryRemove(roomId, out _);
}

Console.WriteLine("P5136 channel-family room filtering smoke test passed.");

(Socket handshakePeer, Socket handshakeServer) = CreateSocketPair();
try
{
    handshakePeer.ReceiveTimeout = 3000;
    SessionGroup handshakeSession = new SessionGroup(handshakeServer, null);

    using (OutPacket prematurePacket = new OutPacket("PrServerTime"))
    {
        prematurePacket.WriteInt(123);
        handshakeSession.Client.Send(prematurePacket);
    }
    Thread.Sleep(25);
    if (handshakePeer.Available != 0)
    {
        Console.Error.WriteLine("P5136 emitted a normal packet before PcFirstMessage.");
        return 1;
    }

    const uint handshakeIv = 0x51365136u;
    byte[] initialPayload;
    using (OutPacket initialPacket = new OutPacket("PcFirstMessage"))
    {
        initialPacket.WriteInt(0x12345678);
        initialPayload = initialPacket.ToArray();
        handshakeSession.Client.SendInitialHandshake(initialPacket, handshakeIv);
    }

    int plaintextLength = BitConverter.ToInt32(ReceiveExact(handshakePeer, 4), 0);
    byte[] receivedInitialPayload = ReceiveExact(handshakePeer, plaintextLength);
    if (plaintextLength != initialPayload.Length ||
        !receivedInitialPayload.SequenceEqual(initialPayload) ||
        handshakeSession.Client.RIV != handshakeIv)
    {
        Console.Error.WriteLine("P5136 initial handshake was not the first plaintext frame with IV ready.");
        return 1;
    }

    byte[] encryptedPayload;
    using (OutPacket encryptedPacket = new OutPacket("PrServerTime"))
    {
        encryptedPacket.WriteInt(0x76543210);
        encryptedPayload = encryptedPacket.ToArray();
        handshakeSession.Client.Send(encryptedPacket);
    }

    uint encryptedHeader = BitConverter.ToUInt32(ReceiveExact(handshakePeer, 4), 0);
    uint encryptedBodyLength = encryptedHeader ^ handshakeIv ^ 4164199944u;
    _ = ReceiveExact(handshakePeer, checked((int)encryptedBodyLength));
    if (encryptedBodyLength != encryptedPayload.Length + 4 ||
        handshakeSession.Client.SIV == handshakeIv)
    {
        Console.Error.WriteLine("P5136 post-handshake frame did not use the negotiated IV.");
        return 1;
    }

    byte[] eventRewardRequestPayload;
    using (OutPacket eventRewardRequest = new OutPacket("LoRqEventRewardPacket"))
    {
        eventRewardRequest.WriteInt(0);
        eventRewardRequest.WriteInt(0);
        eventRewardRequestPayload = eventRewardRequest.ToArray();
    }

    uint eventRewardIv = handshakeSession.Client.SIV;
    using (InPacket eventRewardRequest = new InPacket(eventRewardRequestPayload))
    {
        uint eventRewardHash = eventRewardRequest.ReadUInt();
        if (!Korean5136Protocol.TryHandle(
                handshakeSession,
                eventRewardHash,
                eventRewardRequest))
        {
            Console.Error.WriteLine("P5136 VIP event-reward request was not handled.");
            return 1;
        }
    }

    uint eventRewardHeader = BitConverter.ToUInt32(ReceiveExact(handshakePeer, 4), 0);
    uint eventRewardBodyLength = eventRewardHeader ^ eventRewardIv ^ 4164199944u;
    _ = ReceiveExact(handshakePeer, checked((int)eventRewardBodyLength));
    if (eventRewardBodyLength != 16)
    {
        Console.Error.WriteLine(
            $"P5136 VIP event-reward reply length was {eventRewardBodyLength}, expected 16.");
        return 1;
    }

    handshakeSession.Client.Disconnect();
}
finally
{
    handshakePeer.Dispose();
    handshakeServer.Dispose();
}

Console.WriteLine("P5136 first-frame ordering/encryption and VIP reward pairing smoke test passed.");

string leaseProfileDirectory = Path.Combine(
    Path.GetTempPath(),
    $"KartRider-P5136-leases-{Guid.NewGuid():N}");
string originalProfileDirectory = FileName.ProfileDir;
List<Socket> leasePeerSockets = new List<Socket>();
int leaseRoomId = -1;
try
{
    Directory.CreateDirectory(leaseProfileDirectory);
    FileName.ProfileDir = leaseProfileDirectory;
    FileName.FileNames.Clear();

    string suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
    string primaryNickname = "LeaseA" + suffix;
    string otherNickname = "LeaseB" + suffix;

    (Socket primaryPeer, Socket primaryServer) = CreateSocketPair();
    leasePeerSockets.Add(primaryPeer);
    SessionGroup primary = new SessionGroup(primaryServer, null);
    ClientManager.AddClient(primary);
    if (!ClientManager.TryClaimLoginIdentity(
            primary,
            primaryNickname,
            out string claimedNickname,
            out uint userNo,
            out string claimFailure) ||
        claimedNickname != primaryNickname || userNo == 0)
    {
        Console.Error.WriteLine($"P5136 identity claim failed: {claimFailure}");
        return 1;
    }

    (Socket duplicatePeer, Socket duplicateServer) = CreateSocketPair();
    leasePeerSockets.Add(duplicatePeer);
    SessionGroup duplicate = new SessionGroup(duplicateServer, null);
    ClientManager.AddClient(duplicate);
    if (ClientManager.TryClaimLoginIdentity(
        duplicate,
        primaryNickname.ToLowerInvariant(),
        out _,
        out _,
        out _))
    {
        Console.Error.WriteLine("P5136 duplicate nickname claim was accepted.");
        return 1;
    }
    duplicate.Client.Disconnect();

    (Socket otherPeer, Socket otherServer) = CreateSocketPair();
    leasePeerSockets.Add(otherPeer);
    SessionGroup other = new SessionGroup(otherServer, null);
    ClientManager.AddClient(other);
    if (!ClientManager.TryClaimLoginIdentity(
        other,
        otherNickname,
        out _,
        out uint otherUserNo,
        out _) ||
        otherUserNo == userNo)
    {
        Console.Error.WriteLine("P5136 distinct nickname claim failed.");
        return 1;
    }
    other.Client.Disconnect();

    leaseRoomId = RoomManager.CreateRoom();
    byte leaseSlot = RoomManager.AddPlayer(leaseRoomId, primaryNickname, 0, 0, primary);
    const ushort migrationChannel = 11;
    const byte migrationGameType = 67;
    string migrationBeginFailure = string.Empty;
    if (leaseSlot == byte.MaxValue ||
        !ClientManager.TryBeginChannelMigration(
            primary,
            migrationChannel,
            migrationGameType,
            out ushort migrationToken,
            out migrationBeginFailure))
    {
        Console.Error.WriteLine($"P5136 migration begin failed: {migrationBeginFailure}");
        return 1;
    }

    // Exercise the important ordering: the source socket closes before the
    // channel socket sends PqChannelMovein. Shared room state must survive.
    IDisposable sourceOperation = ClientManager.TryAcquireIdentityOperation(primary);
    if (sourceOperation == null)
    {
        Console.Error.WriteLine("P5136 source operation fence could not be acquired.");
        return 1;
    }
    primary.Client.Disconnect();
    if (RoomManager.TryGetRoomId(primaryNickname) != leaseRoomId)
    {
        Console.Error.WriteLine("P5136 source disconnect removed room state during migration.");
        return 1;
    }

    (Socket destinationPeer, Socket destinationServer) = CreateSocketPair();
    leasePeerSockets.Add(destinationPeer);
    SessionGroup destination = new SessionGroup(destinationServer, null);
    ClientManager.AddClient(destination);
    Task<(bool Success, string Nickname, string Failure)> migrationTask = Task.Run(() =>
    {
        bool success = ClientManager.TryCompleteChannelMigration(
            destination,
            userNo,
            migrationChannel,
            migrationToken,
            out string migrated,
            out string failure);
        return (success, migrated, failure);
    });
    await Task.Delay(75);
    if (migrationTask.IsCompleted)
    {
        Console.Error.WriteLine("P5136 migration did not wait for an in-flight source operation.");
        return 1;
    }
    sourceOperation.Dispose();
    (bool migrationSuccess, string migratedNickname, string migrationFailure) =
        await migrationTask;
    if (!migrationSuccess ||
        migratedNickname != primaryNickname ||
        !ReferenceEquals(ClientManager.GetParent(primaryNickname), destination) ||
        destination.P5136ChannelGameType != migrationGameType ||
        destination.P5136ChannelId != migrationChannel ||
        !ReferenceEquals(RoomManager.GetPlayer(leaseRoomId, primaryNickname)?.Session, destination))
    {
        Console.Error.WriteLine($"P5136 migration ownership transfer failed: {migrationFailure}");
        return 1;
    }

    IDisposable destinationOperation = ClientManager.TryAcquireIdentityOperation(destination);
    if (destinationOperation == null)
    {
        Console.Error.WriteLine("P5136 destination operation fence could not be acquired.");
        return 1;
    }
    destination.Client.Disconnect();

    (Socket blockedPeer, Socket blockedServer) = CreateSocketPair();
    leasePeerSockets.Add(blockedPeer);
    SessionGroup blockedReplacement = new SessionGroup(blockedServer, null);
    ClientManager.AddClient(blockedReplacement);
    if (ClientManager.TryClaimLoginIdentity(
        blockedReplacement,
        primaryNickname,
        out _,
        out _,
        out _))
    {
        Console.Error.WriteLine("P5136 cleanup tombstone allowed an early replacement claim.");
        return 1;
    }
    blockedReplacement.Client.Disconnect();

    destinationOperation.Dispose();
    if (!SpinWait.SpinUntil(
            () => !ClientManager.HasClientWithNickname(primaryNickname) &&
                  RoomManager.TryGetRoomId(primaryNickname) == -1,
            3000))
    {
        Console.Error.WriteLine("P5136 generation cleanup did not finish after its operation fence drained.");
        return 1;
    }

    (Socket replacementPeer, Socket replacementServer) = CreateSocketPair();
    leasePeerSockets.Add(replacementPeer);
    SessionGroup replacement = new SessionGroup(replacementServer, null);
    ClientManager.AddClient(replacement);
    if (!ClientManager.TryClaimLoginIdentity(
            replacement,
            primaryNickname,
            out _,
            out _,
            out string replacementFailure))
    {
        Console.Error.WriteLine($"P5136 post-cleanup replacement claim failed: {replacementFailure}");
        return 1;
    }
    leaseRoomId = RoomManager.CreateRoom();
    if (RoomManager.AddPlayer(leaseRoomId, primaryNickname, 0, 0, replacement) == byte.MaxValue)
    {
        Console.Error.WriteLine("P5136 replacement could not enter a room after cleanup.");
        return 1;
    }
    await Task.Delay(250);
    if (!ReferenceEquals(ClientManager.GetParent(primaryNickname), replacement) ||
        RoomManager.TryGetRoomId(primaryNickname) != leaseRoomId)
    {
        Console.Error.WriteLine("P5136 stale cleanup erased the replacement generation.");
        return 1;
    }
    replacement.Client.Disconnect();
}
finally
{
    ClientManager.DisconnectAll();
    foreach (Socket socket in leasePeerSockets)
        socket.Dispose();
    if (leaseRoomId != -1 && RoomManager.GetRoom(leaseRoomId) is GameRoom leaseRoom)
        RoomManager.RemoveRoom(leaseRoom);
    FileName.FileNames.Clear();
    FileName.ProfileDir = originalProfileDirectory;
    if (Directory.Exists(leaseProfileDirectory))
        Directory.Delete(leaseProfileDirectory, true);
}

Console.WriteLine("P5136 duplicate identity/generation migration smoke test passed.");

string settingsTestDirectory = Path.Combine(
    Path.GetTempPath(),
    $"KartRider-P5136-settings-{Guid.NewGuid():N}");
string settingsTestPath = Path.Combine(settingsTestDirectory, "server-launcher.json");
Profile.Setting originalConnectorSettings = ProfileService.SettingConfig;
try
{
    ProfileService.SettingConfig = new Profile.Setting
    {
        ServerIP = "192.0.2.55",
        ServerPort = 45678
    };

    ServerLauncherSettings defaults = ServerLauncherSettingsStore.LoadOrDefault(settingsTestPath);
    if (defaults.BindAddress != IPAddress.Loopback.ToString() ||
        defaults.AdvertisedAddress != IPAddress.Loopback.ToString() ||
        defaults.ConfiguredPort != topology.DefaultConfiguredPort ||
        defaults.LogDirectory != ServerLauncherSettings.DefaultLogDirectory ||
        !defaults.EnablePacketTrace ||
        File.Exists(settingsTestPath))
    {
        Console.Error.WriteLine("Server settings inherited the connector target.");
        return 1;
    }

    string explicitLogDirectory = Path.Combine(settingsTestDirectory, "trace");
    ServerLauncherSettings explicitSettings = new ServerLauncherSettings
    {
        BindAddress = IPAddress.Any.ToString(),
        AdvertisedAddress = "192.0.2.15",
        ConfiguredPort = 44000,
        LogDirectory = explicitLogDirectory,
        EnablePacketTrace = false,
        FirstMessageDelayMilliseconds = 375,
        ItemProbabilityRankBand = ItemProbabilityRankBand.High,
        IndividualItemProbabilities = new List<ItemProbabilityEntry>
        {
            new ItemProbabilityEntry
            {
                ItemId = 8,
                Name = "banana",
                TopWeight = 1,
                HighWeight = 7,
                MiddleWeight = 1,
                LowWeight = 1
            }
        },
        TeamItemProbabilities = new List<ItemProbabilityEntry>
        {
            new ItemProbabilityEntry
            {
                ItemId = 10,
                Name = "shield",
                TopWeight = 1,
                HighWeight = 9,
                MiddleWeight = 1,
                LowWeight = 1
            }
        },
        RandomTracks = new RandomTrackConfiguration
        {
            Pools = new List<RandomTrackPoolOverride>
            {
                new RandomTrackPoolOverride
                {
                    GameType = 0,
                    Selector = 5,
                    TrackIds = new List<string> { "village_R01", "china_R01" }
                }
            }
        }
    };
    ServerLauncherSettingsStore.Save(explicitSettings, settingsTestPath);
    ServerLauncherSettings reloaded = ServerLauncherSettingsStore.LoadOrDefault(settingsTestPath);
    if (reloaded.BindAddress != explicitSettings.BindAddress ||
        reloaded.AdvertisedAddress != explicitSettings.AdvertisedAddress ||
        reloaded.ConfiguredPort != explicitSettings.ConfiguredPort ||
        reloaded.LogDirectory != explicitSettings.LogDirectory ||
        reloaded.EnablePacketTrace != explicitSettings.EnablePacketTrace ||
        reloaded.FirstMessageDelayMilliseconds != explicitSettings.FirstMessageDelayMilliseconds ||
        reloaded.ItemProbabilityRankBand != ItemProbabilityRankBand.High ||
        reloaded.IndividualItemProbabilities.Count != 1 ||
        reloaded.IndividualItemProbabilities[0].ItemId != 8 ||
        reloaded.IndividualItemProbabilities[0].HighWeight != 7 ||
        reloaded.TeamItemProbabilities.Count != 1 ||
        reloaded.TeamItemProbabilities[0].ItemId != 10 ||
        reloaded.TeamItemProbabilities[0].HighWeight != 9 ||
        reloaded.RandomTracks.Pools.Count != 1 ||
        reloaded.RandomTracks.Pools[0].GameType != 0 ||
        reloaded.RandomTracks.Pools[0].Selector != 5 ||
        !reloaded.RandomTracks.Pools[0].TrackIds.SequenceEqual(
            new[] { "village_R01", "china_R01" }) ||
        reloaded.ToServerOptions(ClientBuildProfiles.Korean5136)
            .RandomTracks.Pools.Count != 1)
    {
        Console.Error.WriteLine("Server settings persistence round trip failed.");
        return 1;
    }
}
finally
{
    ProfileService.SettingConfig = originalConnectorSettings;
    if (Directory.Exists(settingsTestDirectory))
    {
        Directory.Delete(settingsTestDirectory, true);
    }
}

Console.WriteLine("Server/connector settings isolation smoke test passed.");

Korean5136RandomTrackDefinition[] syntheticTracks =
{
    new Korean5136RandomTrackDefinition
    {
        Id = "village_R01",
        KoreanName = "빌리지 고가의 질주",
        GameType = "speed",
        BasicAi = true,
        Hash = Adler32Helper.GenerateAdler32_UNICODE("village_R01", 0)
    },
    new Korean5136RandomTrackDefinition
    {
        Id = "china_R01",
        KoreanName = "차이나 서안 병마용",
        GameType = "speed",
        BasicAi = false,
        Hash = Adler32Helper.GenerateAdler32_UNICODE("china_R01", 0)
    },
    new Korean5136RandomTrackDefinition
    {
        Id = "village_I04",
        KoreanName = "빌리지 운하",
        GameType = "item",
        BasicAi = true,
        Hash = Adler32Helper.GenerateAdler32_UNICODE("village_I04", 0)
    }
};
Korean5136RandomTrackCatalog syntheticRandomCatalog =
    new Korean5136RandomTrackCatalog(
        Path.Combine(Path.GetTempPath(), "synthetic-track_common.rho"),
        syntheticTracks,
        new[]
        {
            new Korean5136RandomTrackPool
            {
                GameType = 0,
                Selector = 5,
                KoreanName = "스피드전 · 인기 랜덤 · 보통",
                DefaultTrackIds = new[] { "village_R01", "china_R01" }
            },
            new Korean5136RandomTrackPool
            {
                GameType = 1,
                Selector = 5,
                KoreanName = "아이템전 · 인기 랜덤 · 보통",
                DefaultTrackIds = new[] { "village_I04" }
            }
        });
RandomTrackConfiguration syntheticRandomOverride = new RandomTrackConfiguration
{
    Pools = new List<RandomTrackPoolOverride>
    {
        new RandomTrackPoolOverride
        {
            GameType = 0,
            Selector = 5,
            TrackIds = new List<string> { "china_R01" }
        }
    }
};
Korean5136RandomTrackService.ConfigureForTesting(
    syntheticRandomCatalog,
    syntheticRandomOverride);
uint chinaTrackHash = Adler32Helper.GenerateAdler32_UNICODE("china_R01", 0);
if (!Korean5136RandomTrackService.TryGetCandidateHashes(
        0,
        5,
        basicAiOnly: false,
        out IReadOnlyList<uint> overriddenRandomTracks) ||
    !overriddenRandomTracks.SequenceEqual(new[] { chinaTrackHash }) ||
    RandomTrack.GetTrackName(chinaTrackHash) != "차이나 서안 병마용" ||
    RandomTrack.GetRandomTrack(null, "random-smoke-override", 0, 5) != chinaTrackHash)
{
    Console.Error.WriteLine("P5136 manual random-track pool application failed.");
    return 1;
}

Korean5136RandomTrackService.ConfigureForTesting(
    syntheticRandomCatalog,
    new RandomTrackConfiguration());
if (!Korean5136RandomTrackService.TryGetCandidateHashes(
        0,
        5,
        basicAiOnly: false,
        out IReadOnlyList<uint> defaultRandomTracks) ||
    defaultRandomTracks.Count != 2)
{
    Console.Error.WriteLine("P5136 client-default random-track pool resolution failed.");
    return 1;
}
uint firstRandomTrack = RandomTrack.GetRandomTrack(
    null,
    "random-smoke-cycle",
    0,
    5);
uint secondRandomTrack = RandomTrack.GetRandomTrack(
    null,
    "random-smoke-cycle",
    0,
    5);
if (firstRandomTrack == secondRandomTrack ||
    !defaultRandomTracks.Contains(firstRandomTrack) ||
    !defaultRandomTracks.Contains(secondRandomTrack) ||
    !RandomTrack.RandomName.TryGetValue(33, out string newLeagueName) ||
    string.IsNullOrWhiteSpace(newLeagueName))
{
    Console.Error.WriteLine("P5136 random-track no-repeat/Korean selector names failed.");
    return 1;
}

try
{
    new RandomTrackConfiguration
    {
        Pools = new List<RandomTrackPoolOverride>
        {
            new RandomTrackPoolOverride
            {
                GameType = 0,
                Selector = 5,
                TrackIds = new List<string> { "village_R01", "village_R01" }
            }
        }
    }.Validate();
    Console.Error.WriteLine("P5136 duplicate random-track id was accepted.");
    return 1;
}
catch (InvalidDataException)
{
}

Korean5136RandomTrackService.SetCatalogOverrideForTesting(syntheticRandomCatalog);
Console.WriteLine("P5136 client/manual random-track pool smoke test passed.");

ItemProbabilityService.Configure(
    AppContext.BaseDirectory,
    new ItemProbabilityConfiguration
    {
        RankBand = ItemProbabilityRankBand.Live,
        Individual = CreateRankSpecificItemTable(),
        Team = CreateRankSpecificItemTable()
    });
short[] expectedLiveRankItems = { 101, 102, 102, 103, 103, 104, 104, 104 };
for (int rank = 0; rank < expectedLiveRankItems.Length; rank++)
{
    short selected = ItemProbabilityService.NextItem(
        teamMode: false,
        liveRank: rank,
        racerCount: expectedLiveRankItems.Length);
    if (selected != expectedLiveRankItems[rank])
    {
        Console.Error.WriteLine(
            $"P5136 live-rank item selection failed: rank={rank}, " +
            $"expected={expectedLiveRankItems[rank]}, actual={selected}");
        return 1;
    }
}

ItemProbabilityService.Configure(
    AppContext.BaseDirectory,
    new ItemProbabilityConfiguration
    {
        RankBand = ItemProbabilityRankBand.Combined,
        Individual = new List<ItemProbabilityEntry>
        {
            new ItemProbabilityEntry
            {
                ItemId = 8,
                Name = "banana",
                TopWeight = 1,
                HighWeight = 1,
                MiddleWeight = 1,
                LowWeight = 1
            }
        },
        Team = new List<ItemProbabilityEntry>
        {
            new ItemProbabilityEntry
            {
                ItemId = 10,
                Name = "shield",
                TopWeight = 1,
                HighWeight = 1,
                MiddleWeight = 1,
                LowWeight = 1
            }
        }
    });
for (int index = 0; index < 32; index++)
{
    if (ItemProbabilityService.NextItem(teamMode: false) != 8 ||
        ItemProbabilityService.NextItem(teamMode: true) != 10)
    {
        Console.Error.WriteLine("P5136 weighted item selection was not deterministic for a one-row table.");
        return 1;
    }
}

Console.WriteLine("P5136 weighted item probability/settings smoke test passed.");

ushort testBasePort = FindAvailablePortBlock();
P5136ServerOptions serverOptions = new P5136ServerOptions
{
    BindAddress = IPAddress.Loopback,
    AdvertisedAddress = IPAddress.Loopback,
    ConfiguredPort = testBasePort,
    LogDirectory = Path.Combine(Path.GetTempPath(), "KartRider-P5136-smoke-logs"),
    EnablePacketTrace = false,
    FirstMessageDelayMilliseconds = 250
};

ClientServerRuntime.Start(AppContext.BaseDirectory, serverOptions);
try
{
    if (!ClientServerRuntime.IsRunning ||
        RouterListener.Listener?.LocalEndpoint is not IPEndPoint loginEndPoint ||
        !loginEndPoint.Address.Equals(IPAddress.Loopback) ||
        loginEndPoint.Port != topology.ResolveLoginTcpPort(testBasePort) ||
        RouterListener.UDPServer?.LocalEndPoint?.Port != topology.ResolveUdpPort(testBasePort) ||
        RouterListener.P2PServer?.LocalEndPoint?.Port != topology.ResolveP2pPort(testBasePort) ||
        RouterListener.MsgrServer?.LocalEndPoint?.Port != topology.ResolveMessengerPort(testBasePort) ||
        ClientServerRuntime.FirstMessageDelayMilliseconds != 250)
    {
        Console.Error.WriteLine("P5136 listener bind smoke test failed.");
        return 1;
    }

    RouterListener.UDPServer.Stop();
    if (ClientServerRuntime.IsRunning || !ClientServerRuntime.HasResources)
    {
        Console.Error.WriteLine("P5136 partial-listener state detection failed.");
        return 1;
    }
}
finally
{
    ClientServerRuntime.Stop();
}

if (ClientServerRuntime.HasResources)
{
    Console.Error.WriteLine("P5136 partial-listener cleanup failed.");
    return 1;
}

ushort conflictBasePort = FindAvailablePortBlock();
using (UdpClient conflict = new UdpClient(new IPEndPoint(
           IPAddress.Loopback,
           topology.ResolveUdpPort(conflictBasePort))))
{
    serverOptions.ConfiguredPort = conflictBasePort;
    bool failedAsExpected = false;
    try
    {
        ClientServerRuntime.Start(AppContext.BaseDirectory, serverOptions);
    }
    catch (SocketException)
    {
        failedAsExpected = true;
    }

    if (!failedAsExpected || ClientServerRuntime.IsRunning)
    {
        ClientServerRuntime.Stop();
        Console.Error.WriteLine("P5136 listener rollback smoke test failed.");
        return 1;
    }
}

Console.WriteLine("P5136 bind/advertise/port lifecycle smoke test passed.");

ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean20051214);
ushort p236BasePort = FindAvailablePortBlock();
serverOptions.BindAddress = IPAddress.Loopback;
serverOptions.AdvertisedAddress = IPAddress.Loopback;
serverOptions.ConfiguredPort = p236BasePort;
ClientServerRuntime.Start(AppContext.BaseDirectory, serverOptions);
try
{
    int expectedPort = ClientBuildProfiles.Korean20051214.Ports.ResolveLoginTcpPort(p236BasePort);
    IPEndPoint p236Tcp = IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveTcpListeners()
        .FirstOrDefault(endpoint => endpoint.Port == expectedPort);
    IPEndPoint p236Udp = IPGlobalProperties.GetIPGlobalProperties()
        .GetActiveUdpListeners()
        .FirstOrDefault(endpoint => endpoint.Port == expectedPort);
    if (p236Tcp == null || p236Udp == null ||
        !p236Tcp.Address.Equals(IPAddress.Loopback) ||
        !p236Udp.Address.Equals(IPAddress.Loopback))
    {
        Console.Error.WriteLine("P236 bind-address compatibility smoke test failed.");
        return 1;
    }
}
finally
{
    ClientServerRuntime.Stop();
    ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean5136);
}

Console.WriteLine("P236 explicit bind-address smoke test passed.");

string p236RestoreTestDirectory = Path.Combine(
    Path.GetTempPath(),
    $"KartRider-P236-restore-{Guid.NewGuid():N}");
string p236RestorePinPath = Path.Combine(p236RestoreTestDirectory, "KartRider.pin");
string p236RestoreGameConfigPath = Path.Combine(p236RestoreTestDirectory, "KartRider.xml");
string p236RestoreProfilePath = Path.Combine(
    p236RestoreTestDirectory,
    ClientBuildProfiles.Korean20051214.ProfileRelativePath);
try
{
    Directory.CreateDirectory(Path.GetDirectoryName(p236RestoreProfilePath)!);
    foreach (string path in new[]
    {
        p236RestorePinPath,
        p236RestoreGameConfigPath,
        p236RestoreProfilePath
    })
    {
        File.WriteAllText(path, "patched", Encoding.UTF8);
        File.WriteAllText(path + ".launcher-v2.bak", "pristine", Encoding.UTF8);
    }

    ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean20051214);
    new LegacyProfileLaunchStrategy().Restore(new ClientLaunchContext(
        p236RestoreTestDirectory,
        p236RestorePinPath,
        Path.Combine(p236RestoreTestDirectory, "KartRider-bak.pin"),
        "127.0.0.1",
        39311,
        "smoke"));

    if (new[] { p236RestorePinPath, p236RestoreGameConfigPath, p236RestoreProfilePath }
        .Any(path =>
            File.ReadAllText(path, Encoding.UTF8) != "pristine" ||
            File.Exists(path + ".launcher-v2.bak")))
    {
        Console.Error.WriteLine("P236 transient restore compatibility test failed.");
        return 1;
    }
}
finally
{
    ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean5136);
    if (Directory.Exists(p236RestoreTestDirectory))
        Directory.Delete(p236RestoreTestDirectory, true);
}

Console.WriteLine("P236 transient restore compatibility smoke test passed.");

string persistentFilesTestDirectory = Path.Combine(
    Path.GetTempPath(),
    $"KartRider-P5136-persistent-files-{Guid.NewGuid():N}");
string persistentGameConfigPath = Path.Combine(persistentFilesTestDirectory, "KartRider.xml");
string persistentLauncherProfilePath = Path.Combine(
    persistentFilesTestDirectory,
    "Profile",
    "kr",
    "launcher.xml");
byte[] pristineGameConfig = Encoding.UTF8.GetBytes(
    "<?xml version='1.0' encoding='UTF-8'?><config><stock value='1'/></config>");
try
{
    Directory.CreateDirectory(persistentFilesTestDirectory);
    File.WriteAllBytes(persistentGameConfigPath, pristineGameConfig);

    LegacyProfileLaunchStrategy.PrepareKorean5136GameConfig(
        persistentGameConfigPath,
        "192.0.2.20",
        46001);
    LegacyProfileLaunchStrategy.PrepareKorean5136LauncherProfile(
        persistentLauncherProfilePath,
        "first-user");

    if (!File.Exists(persistentGameConfigPath + ".pristine.bak") ||
        !pristineGameConfig.SequenceEqual(
            File.ReadAllBytes(persistentGameConfigPath + ".pristine.bak")) ||
        !File.Exists(persistentLauncherProfilePath + ".pristine.absent") ||
        File.Exists(persistentLauncherProfilePath + ".pristine.bak"))
    {
        Console.Error.WriteLine("P5136 persistent XML pristine-state recording failed.");
        return 1;
    }

    LegacyProfileLaunchStrategy.PrepareKorean5136GameConfig(
        persistentGameConfigPath,
        "192.0.2.21",
        46002);
    LegacyProfileLaunchStrategy.PrepareKorean5136LauncherProfile(
        persistentLauncherProfilePath,
        "second-user");

    string liveGameConfig = File.ReadAllText(persistentGameConfigPath, Encoding.UTF8);
    string liveLauncherProfile = File.ReadAllText(persistentLauncherProfilePath, Encoding.UTF8);
    if (!liveGameConfig.Contains("192.0.2.21:46002", StringComparison.Ordinal) ||
        !liveLauncherProfile.Contains("second-user", StringComparison.Ordinal) ||
        !pristineGameConfig.SequenceEqual(
            File.ReadAllBytes(persistentGameConfigPath + ".pristine.bak")) ||
        File.Exists(persistentLauncherProfilePath + ".pristine.bak"))
    {
        Console.Error.WriteLine("P5136 persistent XML repatch/backup test failed.");
        return 1;
    }
}
finally
{
    if (Directory.Exists(persistentFilesTestDirectory))
        Directory.Delete(persistentFilesTestDirectory, true);
}

Console.WriteLine("P5136 persistent XML/pristine-state smoke test passed.");

if (args.Length == 1)
{
    string sourcePinPath = Path.GetFullPath(args[0]);
    byte[] sourcePinBytes = File.ReadAllBytes(sourcePinPath);
    PINFile pin = new PINFile(sourcePinPath);
    if (pin.Header.MinorVersion != 5136)
    {
        Console.Error.WriteLine($"Unexpected PIN protocol: {pin.Header.MinorVersion}");
        return 1;
    }

    string pinTestDirectory = Path.Combine(
        Path.GetTempPath(),
        $"KartRider-P5136-pin-{Guid.NewGuid():N}");
    string pinTestPath = Path.Combine(pinTestDirectory, "KartRider.pin");
    string pristinePinBackupPath = pinTestPath + ".pristine.bak";
    const string expectedIp = "192.0.2.10";
    const ushort expectedPort = 45001;
    const string secondExpectedIp = "192.0.2.11";
    const ushort secondExpectedPort = 45002;
    const string migratedExpectedIp = "192.0.2.12";
    const ushort migratedExpectedPort = 45003;
    try
    {
        Directory.CreateDirectory(pinTestDirectory);
        File.Copy(sourcePinPath, pinTestPath);
        LegacyProfileLaunchStrategy.PrepareKorean5136Pin(
            pinTestPath,
            expectedIp,
            expectedPort);

        PINFile patched = new PINFile(pinTestPath);
        if (patched.AuthMethods == null ||
            patched.AuthMethods.Count == 0 ||
            patched.AuthMethods.Any(authMethod =>
                authMethod?.LoginServers == null ||
                authMethod.LoginServers.Count != 1 ||
                authMethod.LoginServers[0].IP != expectedIp ||
                authMethod.LoginServers[0].Port != expectedPort))
        {
            Console.Error.WriteLine("P5136 PIN endpoint patch smoke test failed.");
            return 1;
        }
        if (!File.Exists(pristinePinBackupPath) ||
            !sourcePinBytes.SequenceEqual(File.ReadAllBytes(pristinePinBackupPath)) ||
            File.Exists(pinTestPath + ".launcher-v2.bak"))
        {
            Console.Error.WriteLine("P5136 pristine PIN backup creation failed.");
            return 1;
        }

        LegacyProfileLaunchStrategy.PrepareKorean5136Pin(
            pinTestPath,
            secondExpectedIp,
            secondExpectedPort);
        PINFile secondPatched = new PINFile(pinTestPath);
        if (secondPatched.AuthMethods == null ||
            secondPatched.AuthMethods.Count == 0 ||
            secondPatched.AuthMethods.Any(authMethod =>
                authMethod?.LoginServers == null ||
                authMethod.LoginServers.Count != 1 ||
                authMethod.LoginServers[0].IP != secondExpectedIp ||
                authMethod.LoginServers[0].Port != secondExpectedPort))
        {
            Console.Error.WriteLine("P5136 persistent PIN repatch smoke test failed.");
            return 1;
        }
        if (!sourcePinBytes.SequenceEqual(File.ReadAllBytes(pristinePinBackupPath)))
        {
            Console.Error.WriteLine("P5136 pristine PIN backup was overwritten.");
            return 1;
        }

        string migratedPinPath = Path.Combine(pinTestDirectory, "Migrated-KartRider.pin");
        File.Copy(pinTestPath, migratedPinPath);
        File.Copy(sourcePinPath, migratedPinPath + ".launcher-v2.bak");
        LegacyProfileLaunchStrategy.PrepareKorean5136Pin(
            migratedPinPath,
            migratedExpectedIp,
            migratedExpectedPort);
        PINFile migratedPin = new PINFile(migratedPinPath);
        if (!File.Exists(migratedPinPath + ".pristine.bak") ||
            !sourcePinBytes.SequenceEqual(
                File.ReadAllBytes(migratedPinPath + ".pristine.bak")) ||
            File.Exists(migratedPinPath + ".launcher-v2.bak") ||
            migratedPin.AuthMethods.Any(authMethod =>
                authMethod?.LoginServers == null ||
                authMethod.LoginServers.Count != 1 ||
                authMethod.LoginServers[0].IP != migratedExpectedIp ||
                authMethod.LoginServers[0].Port != migratedExpectedPort))
        {
            Console.Error.WriteLine("P5136 legacy backup migration smoke test failed.");
            return 1;
        }

        byte[] persistentPinBytes = File.ReadAllBytes(pinTestPath);

        new LegacyProfileLaunchStrategy().Restore(new ClientLaunchContext(
            pinTestDirectory,
            pinTestPath,
            Path.Combine(pinTestDirectory, "KartRider-bak.pin"),
            expectedIp,
            checked((ushort)(expectedPort - 1)),
            "smoke"));
        if (!persistentPinBytes.SequenceEqual(File.ReadAllBytes(pinTestPath)))
        {
            Console.Error.WriteLine("P5136 persistent PIN was restored unexpectedly.");
            return 1;
        }
        if (!sourcePinBytes.SequenceEqual(File.ReadAllBytes(sourcePinPath)))
        {
            Console.Error.WriteLine("P5136 PIN source fixture was modified.");
            return 1;
        }
    }
    finally
    {
        if (Directory.Exists(pinTestDirectory))
            Directory.Delete(pinTestDirectory, true);
    }

    Console.WriteLine("P5136 persistent PIN/pristine-backup smoke test passed.");
}

return 0;

static bool RunPacketTraceSmokeTests(out string failure)
{
    if (!RunUiLogBatchSmoke(out failure) ||
        !RunBasicPacketTraceSmoke(out failure) ||
        !RunConcurrentPacketTraceSmoke(out failure) ||
        !RunPacketTraceQueueCapacitySmoke(out failure) ||
        !RunPacketTraceBlockedUiSmoke(out failure) ||
        !RunPacketTraceWriterRecoverySmoke(out failure))
    {
        return false;
    }

    failure = string.Empty;
    return true;
}

static bool RunUdpEndpointBindingSmokeTests(out string failure)
{
    const string nickname = "UdpFirstBind";
    IPEndPoint udpFirst = new IPEndPoint(IPAddress.Parse("192.0.2.15"), 51000);
    IPEndPoint udpAlternate = new IPEndPoint(IPAddress.Parse("192.0.2.15"), 51001);
    IPEndPoint p2pFirst = new IPEndPoint(IPAddress.Parse("192.0.2.15"), 52000);

    UdpServer.ClearEndpointBindingsForTesting();
    try
    {
        if (UdpServer.BindEndpointForTesting(
                nickname, false, udpFirst, 0x11111111, false, 10, out _) !=
            EndpointBindResult.Bound ||
            UdpServer.BindEndpointForTesting(
                nickname, true, p2pFirst, 0x22222222, true, 10, out _) !=
            EndpointBindResult.Bound)
        {
            failure = "the first UDP and P2P routes were not bound independently";
            return false;
        }

        if (UdpServer.BindEndpointForTesting(
                nickname, false, udpFirst, 0x33333333, false, 10,
                out GenerationEndpointBinding refreshed) != EndpointBindResult.Refreshed ||
            refreshed.Hash != 0x33333333 ||
            UdpServer.BindEndpointForTesting(
                nickname, false, udpAlternate, 0x44444444, false, 10,
                out GenerationEndpointBinding rejected) != EndpointBindResult.EndpointMismatch ||
            !rejected.Endpoint.Equals(udpFirst))
        {
            failure = "a same-generation alternate endpoint replaced the first UDP route";
            return false;
        }

        if (UdpServer.BindEndpointForTesting(
                nickname, false, udpAlternate, 0x55555555, false, 11,
                out GenerationEndpointBinding advanced) != EndpointBindResult.AdvancedGeneration ||
            !advanced.Endpoint.Equals(udpAlternate) ||
            UdpServer.BindEndpointForTesting(
                nickname, false, udpFirst, 0x66666666, false, 10, out _) !=
            EndpointBindResult.StaleGeneration)
        {
            failure = "generation advancement/stale rejection was incorrect";
            return false;
        }

        const string raceNickname = "UdpFirstBindRace";
        var results = new ConcurrentBag<EndpointBindResult>();
        using Barrier barrier = new Barrier(2);
        Parallel.Invoke(
            () =>
            {
                barrier.SignalAndWait();
                results.Add(UdpServer.BindEndpointForTesting(
                    raceNickname,
                    false,
                    udpFirst,
                    1,
                    false,
                    20,
                    out _));
            },
            () =>
            {
                barrier.SignalAndWait();
                results.Add(UdpServer.BindEndpointForTesting(
                    raceNickname,
                    false,
                    udpAlternate,
                    2,
                    false,
                    20,
                    out _));
            });

        if (results.Count(result => result == EndpointBindResult.Bound) != 1 ||
            results.Count(result => result == EndpointBindResult.EndpointMismatch) != 1 ||
            !UdpServer.TryGetEndpointForTesting(
                raceNickname,
                false,
                out GenerationEndpointBinding raceWinner) ||
            (!raceWinner.Endpoint.Equals(udpFirst) &&
             !raceWinner.Endpoint.Equals(udpAlternate)))
        {
            failure = "concurrent first bind did not produce exactly one winner";
            return false;
        }

        failure = string.Empty;
        return true;
    }
    finally
    {
        UdpServer.ClearEndpointBindingsForTesting();
    }
}

static bool RunItemPickupContextSmokeTests(out string failure)
{
    byte[] capturedData = Convert.FromHexString(
        "25080000F025770002017D00023C1CF343F69EED4328367D41");
    ItemPickupContext pickup = SlotData.ParseItemPickupContext(capturedData, 5);
    if (pickup.LiveRank != 5 ||
        pickup.X < 486f || pickup.X >= 487f ||
        pickup.Y < 475f || pickup.Y >= 476f ||
        pickup.Z < 15f || pickup.Z >= 16f)
    {
        failure =
            $"captured GameSlot context decoded as rank={pickup.LiveRank}, " +
            $"xyz=({pickup.X}, {pickup.Y}, {pickup.Z})";
        return false;
    }

    ItemProbabilityRankBand[] expectedEightRacerBands =
    {
        ItemProbabilityRankBand.Top,
        ItemProbabilityRankBand.High,
        ItemProbabilityRankBand.High,
        ItemProbabilityRankBand.Middle,
        ItemProbabilityRankBand.Middle,
        ItemProbabilityRankBand.Low,
        ItemProbabilityRankBand.Low,
        ItemProbabilityRankBand.Low
    };
    for (int rank = 0; rank < expectedEightRacerBands.Length; rank++)
    {
        ItemProbabilityRankBand actual = ItemProbabilityService.ResolveRankBand(
            ItemProbabilityRankBand.Live,
            rank,
            expectedEightRacerBands.Length);
        if (actual != expectedEightRacerBands[rank])
        {
            failure =
                $"rank {rank} mapped to {actual}, expected {expectedEightRacerBands[rank]}";
            return false;
        }
    }

    if (ItemProbabilityService.ResolveRankBand(
            ItemProbabilityRankBand.Live,
            -1,
            8) != ItemProbabilityRankBand.Combined ||
        ItemProbabilityService.ResolveRankBand(
            ItemProbabilityRankBand.High,
            7,
            8) != ItemProbabilityRankBand.High)
    {
        failure = "missing live context or a fixed UI override did not preserve its fallback";
        return false;
    }

    failure = string.Empty;
    return true;
}

static bool RunBarricadePlacementSmokeTests(out string failure)
{
    (uint ObjectId, float X, float Y, float Z)[] expected =
    {
        (0x20001001, 12.5f, -8.25f, 3.75f),
        (0x20001002, 18.125f, 4.5f, 6.25f),
        (0x20001003, -2.75f, 11.5f, 9.125f)
    };
    byte[][] syntheticPackets = expected
        .Select((placement, index) => CreateSyntheticBarricadePlacementPacket(
            placement.ObjectId,
            123456U + (uint)index,
            placement.X,
            placement.Y,
            placement.Z))
        .ToArray();

    ClientBuildProfile previousProfile = ClientBuildProfiles.Active;
    try
    {
        ClientBuildProfiles.SetActive(ClientBuildProfiles.Korean5136);
        for (int index = 0; index < syntheticPackets.Length; index++)
        {
            byte[] packet = syntheticPackets[index];
            if (!SlotData.TryResolveSenderInclusivePlacement(
                    12,
                    packet,
                    out BarricadePlacementContext placement) ||
                placement.PlayerId != 17 ||
                placement.OwnerId != 17 ||
                placement.ObjectId != expected[index].ObjectId ||
                MathF.Abs(placement.X - expected[index].X) > 0.01f ||
                MathF.Abs(placement.Y - expected[index].Y) > 0.01f ||
                MathF.Abs(placement.Z - expected[index].Z) > 0.01f)
            {
                failure = $"synthetic placement {index} was not decoded correctly";
                return false;
            }
        }

        byte[] validPacket = syntheticPackets[0];
        byte[] wrongHash = validPacket.ToArray();
        wrongHash[20] ^= 0x01;
        byte[] wrongLength = validPacket.ToArray();
        wrongLength[16] = 72;
        byte[] wrongOwner = validPacket.ToArray();
        wrongOwner[37] ^= 0x01;
        byte[] truncated = validPacket[..^1];
        if (SlotData.TryResolveSenderInclusivePlacement(9, validPacket, out _) ||
            SlotData.TryResolveSenderInclusivePlacement(12, wrongHash, out _) ||
            SlotData.TryResolveSenderInclusivePlacement(12, wrongLength, out _) ||
            SlotData.TryResolveSenderInclusivePlacement(12, wrongOwner, out _) ||
            SlotData.TryResolveSenderInclusivePlacement(12, truncated, out _))
        {
            failure = "non-barricade or malformed data entered the sender-inclusive path";
            return false;
        }

        ClientBuildProfiles.SetActive(ClientBuildProfiles.Modern);
        if (SlotData.TryResolveSenderInclusivePlacement(12, validPacket, out _))
        {
            failure = "the P5136 sender-inclusive path leaked into the modern protocol";
            return false;
        }

        failure = string.Empty;
        return true;
    }
    finally
    {
        ClientBuildProfiles.SetActive(previousProfile);
    }
}

static byte[] CreateSyntheticBarricadePlacementPacket(
    uint objectId,
    uint tick,
    float x,
    float y,
    float z)
{
    byte[] packet = new byte[93];
    void Write(int offset, byte[] value) => value.CopyTo(packet, offset);

    Write(0, BitConverter.GetBytes(0x27C00574U));
    Write(4, BitConverter.GetBytes(17));
    Write(8, BitConverter.GetBytes(2));
    packet[12] = 12;
    packet[13] = 0x42;
    Write(16, BitConverter.GetBytes(73U));
    Write(20, BitConverter.GetBytes(0x1D8604A3U));
    Write(24, BitConverter.GetBytes(0x2D0605C2U));
    Write(28, BitConverter.GetBytes(objectId));
    packet[32] = 1;
    Write(33, BitConverter.GetBytes(tick));
    Write(37, BitConverter.GetBytes(17));
    Write(45, BitConverter.GetBytes(x));
    Write(49, BitConverter.GetBytes(y));
    Write(53, BitConverter.GetBytes(z));

    float[] identityTransform =
    {
        1F, 0F, 0F,
        0F, 1F, 0F,
        0F, 0F, 1F
    };
    for (int index = 0; index < identityTransform.Length; index++)
    {
        Write(57 + index * sizeof(float), BitConverter.GetBytes(identityTransform[index]));
    }

    return packet;
}

static List<ItemProbabilityEntry> CreateRankSpecificItemTable()
{
    return new List<ItemProbabilityEntry>
    {
        new ItemProbabilityEntry { ItemId = 101, Name = "top", TopWeight = 1 },
        new ItemProbabilityEntry { ItemId = 102, Name = "high", HighWeight = 1 },
        new ItemProbabilityEntry { ItemId = 103, Name = "middle", MiddleWeight = 1 },
        new ItemProbabilityEntry { ItemId = 104, Name = "low", LowWeight = 1 }
    };
}

static bool RunUiLogBatchSmoke(out string failure)
{
    List<string> writes = new List<string>();
    UiLogTextWriter writer = new UiLogTextWriter(writes.Add, "[trace] ");
    writer.Write("one\ntwo\nthree\n");
    string expected = string.Join(
        Environment.NewLine,
        "[trace] one",
        "[trace] two",
        "[trace] three");
    if (writes.Count != 1 || writes[0] != expected)
    {
        failure = "a bounded Console batch was split into multiple WinForms updates";
        return false;
    }

    failure = string.Empty;
    return true;
}

static bool RunBasicPacketTraceSmoke(out string failure)
{
    string directory = CreateTraceTestDirectory("basic");
    TextWriter originalOut = Console.Out;
    StringWriter traceConsole = new StringWriter();
    string path = string.Empty;
    try
    {
        Console.SetOut(traceConsole);
        PacketTrace.Configure(true, directory);
        path = PacketTrace.TracePath;
        byte[] payload = CreateLoginTracePayload();
        PacketTrace.LogPacket(
            "TCP",
            "RX",
            new IPEndPoint(IPAddress.Loopback, 39312),
            new IPEndPoint(IPAddress.Loopback, 50000),
            "TraceUser",
            payload,
            0,
            "smoke=true",
            payload);
        byte[] streamingPayload = Enumerable.Range(0, 3000)
            .Select(index => (byte)(index % 251))
            .ToArray();
        PacketTrace.LogPacket(
            "UDP",
            "TX",
            null,
            null,
            "TraceUser",
            streamingPayload,
            -1,
            "streaming-hex=true");
        PacketTrace.LogDetailEvent(
            "LOGIN-TCP",
            "ITEM-PICKUP-DETAIL",
            null,
            null,
            "TraceUser",
            "rank=2; xyz=(1,2,3)");
        PacketTrace.Stop();
    }
    catch (Exception exception)
    {
        failure = $"basic trace threw {exception}";
        return false;
    }
    finally
    {
        PacketTrace.Stop();
        Console.SetOut(originalOut);
    }

    try
    {
        string uiOutput = traceConsole.ToString();
        string fileOutput = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        string expectedStreamingHex =
            "HEX  | " +
            BitConverter.ToString(
                Enumerable.Range(0, 3000)
                    .Select(index => (byte)(index % 251))
                    .ToArray())
                .Replace("-", " ");
        if (uiOutput.Contains("HEX  |", StringComparison.Ordinal) ||
            uiOutput.Contains("WIRE |", StringComparison.Ordinal) ||
            uiOutput.Contains("ITEM-PICKUP-DETAIL", StringComparison.Ordinal) ||
            !uiOutput.Contains("name=PqLogin", StringComparison.Ordinal) ||
            !fileOutput.Contains("HEX  |", StringComparison.Ordinal) ||
            !fileOutput.Contains("WIRE |", StringComparison.Ordinal) ||
            !fileOutput.Contains("ITEM-PICKUP-DETAIL", StringComparison.Ordinal) ||
            !fileOutput.Contains(expectedStreamingHex, StringComparison.Ordinal) ||
            !fileOutput.Contains("# stopped=", StringComparison.Ordinal))
        {
            failure = "basic UI/file separation or drain footer was incorrect";
            return false;
        }

        failure = string.Empty;
        return true;
    }
    finally
    {
        DeleteTraceTestDirectory(directory);
    }
}

static bool RunConcurrentPacketTraceSmoke(out string failure)
{
    string directory = CreateTraceTestDirectory("concurrent");
    TextWriter originalOut = Console.Out;
    string path = string.Empty;
    try
    {
        Console.SetOut(TextWriter.Null);
        PacketTrace.Configure(true, directory);
        path = PacketTrace.TracePath;
        Parallel.For(0, 8, producer =>
        {
            byte[] payload = CreateLoginTracePayload();
            for (int index = 0; index < 300; index++)
            {
                PacketTrace.LogPacket(
                    producer % 2 == 0 ? "TCP" : "UDP",
                    index % 2 == 0 ? "RX" : "TX",
                    new IPEndPoint(IPAddress.Loopback, 39312 + producer),
                    new IPEndPoint(IPAddress.Loopback, 50000 + producer),
                    $"Trace{producer}",
                    payload,
                    0,
                    $"producer={producer}; index={index}",
                    payload);
            }
        });
        PacketTrace.Stop();
    }
    catch (Exception exception)
    {
        failure = $"concurrent trace threw {exception}";
        return false;
    }
    finally
    {
        PacketTrace.Stop();
        Console.SetOut(originalOut);
    }

    try
    {
        string[] lines = File.Exists(path) ? File.ReadAllLines(path) : Array.Empty<string>();
        long previousSequence = 0;
        foreach (string line in lines)
        {
            if (!line.Contains(" | seq=", StringComparison.Ordinal))
            {
                continue;
            }

            if (!TryReadTraceSequence(line, out long sequence) || sequence <= previousSequence)
            {
                failure = $"file sequence was not strictly increasing: previous={previousSequence}, line={line}";
                return false;
            }
            previousSequence = sequence;
        }

        string footer = lines.LastOrDefault(line => line.StartsWith("# stopped=", StringComparison.Ordinal));
        if (footer == null ||
            !TryReadFooterMetric(footer, "attempted", out long attempted) ||
            !TryReadFooterMetric(footer, "enqueued", out long enqueued) ||
            !TryReadFooterMetric(footer, "written", out long written) ||
            !TryReadFooterMetric(footer, "dropped", out long dropped) ||
            !TryReadFooterMetric(footer, "shutdownRejected", out long shutdownRejected) ||
            written != enqueued ||
            attempted != enqueued + dropped + shutdownRejected)
        {
            failure = $"concurrent footer accounting was inconsistent: {footer ?? "<missing>"}";
            return false;
        }

        failure = string.Empty;
        return true;
    }
    finally
    {
        DeleteTraceTestDirectory(directory);
    }
}

static bool RunPacketTraceQueueCapacitySmoke(out string failure)
{
    string directory = CreateTraceTestDirectory("capacity");
    TextWriter originalOut = Console.Out;
    bool writerPaused = false;
    try
    {
        Console.SetOut(TextWriter.Null);
        PacketTrace.Configure(true, directory);
        writerPaused = PacketTrace.PauseDetailWriterForTesting(2000);
        if (!writerPaused)
        {
            failure = "detail writer did not enter the deterministic test pause";
            return false;
        }

        byte[] payload = CreateLoginTracePayload();
        const int attempts = 3000;
        for (int index = 0; index < attempts; index++)
        {
            PacketTrace.LogPacket(
                "TCP",
                "RX",
                null,
                null,
                "Capacity",
                payload,
                0,
                $"index={index}",
                payload);
        }

        PacketTrace.PacketTraceDiagnostics diagnostics =
            PacketTrace.GetDiagnosticsForTesting();
        if (diagnostics.Attempted != attempts ||
            diagnostics.DetailDropped == 0 ||
            diagnostics.Enqueued + diagnostics.DetailDropped != attempts ||
            diagnostics.PacketSnapshots != diagnostics.Enqueued)
        {
            failure =
                "bounded admission copied dropped payloads or miscounted records: " +
                $"attempted={diagnostics.Attempted}, enqueued={diagnostics.Enqueued}, " +
                $"dropped={diagnostics.DetailDropped}, snapshots={diagnostics.PacketSnapshots}";
            return false;
        }

        failure = string.Empty;
        return true;
    }
    catch (Exception exception)
    {
        failure = $"capacity trace threw {exception}";
        return false;
    }
    finally
    {
        if (writerPaused)
        {
            PacketTrace.ResumeDetailWriterForTesting();
        }
        PacketTrace.Stop();
        Console.SetOut(originalOut);
        DeleteTraceTestDirectory(directory);
    }
}

static bool RunPacketTraceBlockedUiSmoke(out string failure)
{
    string directory = CreateTraceTestDirectory("blocked-ui");
    TextWriter originalOut = Console.Out;
    TextWriter originalError = Console.Error;
    BlockingTextWriter blockingWriter = new BlockingTextWriter();
    try
    {
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        PacketTrace.Configure(true, directory);
        Console.SetOut(blockingWriter);
        PacketTrace.LogEvent("TCP", "BLOCK-UI", null, null, null, "smoke=true");
        if (!blockingWriter.WaitUntilWriteEntered(2000))
        {
            failure = "UI summary worker did not reach the blocking writer";
            return false;
        }

        Stopwatch producerTimer = Stopwatch.StartNew();
        byte[] payload = CreateLoginTracePayload();
        for (int index = 0; index < 100; index++)
        {
            PacketTrace.LogPacket(
                "TCP", "RX", null, null, "BlockedUi", payload, 0, null, payload);
        }
        producerTimer.Stop();
        if (producerTimer.Elapsed > TimeSpan.FromMilliseconds(500))
        {
            failure = $"network-side logging blocked on UI output for {producerTimer.Elapsed}";
            return false;
        }

        Stopwatch stopTimer = Stopwatch.StartNew();
        PacketTrace.Stop();
        stopTimer.Stop();
        if (stopTimer.Elapsed > TimeSpan.FromSeconds(2))
        {
            failure = $"bounded shutdown waited too long for blocked UI: {stopTimer.Elapsed}";
            return false;
        }

        failure = string.Empty;
        return true;
    }
    catch (Exception exception)
    {
        failure = $"blocked-UI trace threw {exception}";
        return false;
    }
    finally
    {
        blockingWriter.Release();
        SpinWait.SpinUntil(() => blockingWriter.CompletedWrites >= 3, 1000);
        PacketTrace.Stop();
        Console.SetOut(originalOut);
        Console.SetError(originalError);
        DeleteTraceTestDirectory(directory);
    }
}

static bool RunPacketTraceWriterRecoverySmoke(out string failure)
{
    string directory = CreateTraceTestDirectory("recovery");
    TextWriter originalOut = Console.Out;
    TextWriter originalError = Console.Error;
    try
    {
        Console.SetOut(TextWriter.Null);
        Console.SetError(TextWriter.Null);
        PacketTrace.Configure(true, directory);
        PacketTrace.InjectWriterFailureForTesting();
        if (!SpinWait.SpinUntil(() => !PacketTrace.Enabled, 3000))
        {
            failure = "writer failure did not disable the active trace worker";
            return false;
        }

        PacketTrace.Configure(true, directory);
        string restartedPath = PacketTrace.TracePath;
        byte[] payload = CreateLoginTracePayload();
        PacketTrace.LogPacket(
            "TCP", "RX", null, null, "Recovery", payload, 0, "restart=true", payload);
        PacketTrace.Stop();
        string restartedFile = File.Exists(restartedPath)
            ? File.ReadAllText(restartedPath)
            : string.Empty;
        if (!restartedFile.Contains("name=PqLogin", StringComparison.Ordinal) ||
            !restartedFile.Contains("# stopped=", StringComparison.Ordinal))
        {
            failure = "trace worker did not restart cleanly after writer failure";
            return false;
        }

        failure = string.Empty;
        return true;
    }
    catch (Exception exception)
    {
        failure = $"writer recovery trace threw {exception}";
        return false;
    }
    finally
    {
        PacketTrace.Stop();
        Console.SetOut(originalOut);
        Console.SetError(originalError);
        DeleteTraceTestDirectory(directory);
    }
}

static byte[] CreateLoginTracePayload()
{
    return BitConverter.GetBytes(Adler32Helper.GenerateAdler32_ASCII("PqLogin", 0));
}

static string CreateTraceTestDirectory(string label)
{
    return Path.Combine(
        Path.GetTempPath(),
        $"KartRider-P5136-trace-{label}-{Guid.NewGuid():N}");
}

static void DeleteTraceTestDirectory(string directory)
{
    if (Directory.Exists(directory))
    {
        Directory.Delete(directory, true);
    }
}

static bool TryReadTraceSequence(string line, out long sequence)
{
    const string marker = " | seq=";
    int start = line.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
    {
        sequence = 0;
        return false;
    }

    start += marker.Length;
    int end = line.IndexOf(' ', start);
    string value = end < 0 ? line.Substring(start) : line.Substring(start, end - start);
    return long.TryParse(value, out sequence);
}

static bool TryReadFooterMetric(string footer, string name, out long value)
{
    string marker = name + "=";
    int start = footer.IndexOf(marker, StringComparison.Ordinal);
    if (start < 0)
    {
        value = 0;
        return false;
    }

    start += marker.Length;
    int end = footer.IndexOf(' ', start);
    string text = end < 0 ? footer.Substring(start) : footer.Substring(start, end - start);
    return long.TryParse(text, out value);
}

static bool RunRoomNameSpeedKeywordSmokeTests(out string failure)
{
    (string RoomName, string Version, byte Speed, byte Infinite)[] cases =
    {
        ("[S0] 보통 방", "国服", 3, byte.MaxValue),
        ("s1 연습", "国服", 0, byte.MaxValue),
        ("빠른방-S2", "国服", 1, byte.MaxValue),
        ("S3_고속", "国服", 2, byte.MaxValue),
        ("[S4] 무한부스터", "国服", 4, 4),
        ("S6 진무한", "国服", 6, 6),
        ("S7 통합 스피드", "国服", 7, byte.MaxValue),
        ("S8 통합 아이템", "国服", 8, byte.MaxValue),
        ("KR-L2 클래식", "韩服复古", 3, byte.MaxValue),
        ("PRO 클래식", "国服复古", 5, byte.MaxValue)
    };

    Dictionary<string, byte> currentSpeeds = SpeedType.speedNames["国服"];
    byte[] expectedSpeedValues = { 3, 0, 1, 2, 4, 5, 6, 7, 8 };
    for (int grade = 0; grade <= 8; grade++)
    {
        if (!currentSpeeds.TryGetValue($"S{grade}", out byte value) ||
            value != expectedSpeedValues[grade])
        {
            failure = $"S{grade} dropdown mapping";
            return false;
        }
    }

    if (TrackRankData.GetSpeedTypeName(7) != "S7 통합 스피드" ||
        TrackRankData.GetSpeedTypeName(8) != "S8 통합 아이템")
    {
        failure = "S7/S8 Korean display labels";
        return false;
    }

    foreach ((string roomName, string version, byte speed, byte infinite) in cases)
    {
        var parsed = SpeedType.Parse(roomName);
        if (!parsed.HasValue ||
            parsed.Value.version != version ||
            parsed.Value.speed != speed ||
            parsed.Value.infinite != infinite)
        {
            failure = roomName;
            return false;
        }
    }

    if (SpeedType.Parse("普通 无限 구형 키워드").HasValue ||
        SpeedType.Parse("TESTS1ROOM").HasValue ||
        SpeedType.Parse("S10").HasValue)
    {
        failure = "legacy or embedded keyword was accepted";
        return false;
    }

    failure = null;
    return true;
}

static (Socket Client, Socket Server) CreateSocketPair()
{
    TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    try
    {
        Task<Socket> acceptTask = listener.AcceptSocketAsync();
        Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect((IPEndPoint)listener.LocalEndpoint);
        Socket server = acceptTask.GetAwaiter().GetResult();
        return (client, server);
    }
    finally
    {
        listener.Stop();
    }
}

static byte[] ReceiveExact(Socket socket, int length)
{
    byte[] buffer = new byte[length];
    int offset = 0;
    while (offset < buffer.Length)
    {
        int received = socket.Receive(buffer, offset, buffer.Length - offset, SocketFlags.None);
        if (received <= 0)
            throw new EndOfStreamException("Socket closed before the expected frame was received.");
        offset += received;
    }
    return buffer;
}

static ushort FindAvailablePortBlock()
{
    for (int candidate = 43000; candidate <= 62000; candidate += 3)
    {
        TcpListener login = null;
        TcpListener messenger = null;
        UdpClient game = null;
        UdpClient p2p = null;
        try
        {
            login = new TcpListener(IPAddress.Loopback, candidate + 1);
            messenger = new TcpListener(IPAddress.Loopback, candidate + 2);
            game = new UdpClient(new IPEndPoint(IPAddress.Loopback, candidate));
            p2p = new UdpClient(new IPEndPoint(IPAddress.Loopback, candidate + 1));
            login.Start();
            messenger.Start();
            return checked((ushort)candidate);
        }
        catch (SocketException)
        {
        }
        finally
        {
            login?.Stop();
            messenger?.Stop();
            game?.Dispose();
            p2p?.Dispose();
        }
    }

    throw new InvalidOperationException("Could not find an available P5136 port block for the smoke test.");
}

sealed class BlockingTextWriter : TextWriter
{
    private readonly ManualResetEventSlim entered = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim released = new ManualResetEventSlim(false);
    private int completedWrites;

    public override Encoding Encoding => Encoding.UTF8;

    public override void Write(string value)
    {
        entered.Set();
        released.Wait();
        Interlocked.Increment(ref completedWrites);
    }

    public override void Write(char value)
    {
        Write(value.ToString());
    }

    public bool WaitUntilWriteEntered(int timeoutMilliseconds)
    {
        return entered.Wait(timeoutMilliseconds);
    }

    public int CompletedWrites => Volatile.Read(ref completedWrites);

    public void Release()
    {
        released.Set();
    }
}
