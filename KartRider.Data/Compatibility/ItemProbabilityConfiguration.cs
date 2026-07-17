using KartLibrary.File;
using KartLibrary.Xml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KartRider.Compatibility
{
    public enum ItemProbabilityRankBand
    {
        Live = 0,
        Top = 1,
        High = 2,
        Middle = 3,
        Low = 4,
        Combined = 5
    }

    public sealed class ItemProbabilityEntry
    {
        public short ItemId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int TopWeight { get; set; }

        public int HighWeight { get; set; }

        public int MiddleWeight { get; set; }

        public int LowWeight { get; set; }

        public ItemProbabilityEntry Clone()
        {
            return new ItemProbabilityEntry
            {
                ItemId = ItemId,
                Name = Name,
                TopWeight = TopWeight,
                HighWeight = HighWeight,
                MiddleWeight = MiddleWeight,
                LowWeight = LowWeight
            };
        }

        public long GetWeight(ItemProbabilityRankBand rankBand)
        {
            return rankBand switch
            {
                ItemProbabilityRankBand.Top => TopWeight,
                ItemProbabilityRankBand.High => HighWeight,
                ItemProbabilityRankBand.Middle => MiddleWeight,
                ItemProbabilityRankBand.Low => LowWeight,
                _ => (long)TopWeight + HighWeight + MiddleWeight + LowWeight
            };
        }
    }

    public sealed class ItemProbabilityConfiguration
    {
        public ItemProbabilityRankBand RankBand { get; set; } =
            ItemProbabilityRankBand.Live;

        public List<ItemProbabilityEntry> Individual { get; set; } =
            new List<ItemProbabilityEntry>();

        public List<ItemProbabilityEntry> Team { get; set; } =
            new List<ItemProbabilityEntry>();

        public ItemProbabilityConfiguration Clone()
        {
            return new ItemProbabilityConfiguration
            {
                RankBand = RankBand,
                Individual = CloneEntries(Individual),
                Team = CloneEntries(Team)
            };
        }

        internal static List<ItemProbabilityEntry> CloneEntries(
            IEnumerable<ItemProbabilityEntry> entries)
        {
            return entries?.Select(entry => entry.Clone()).ToList()
                ?? new List<ItemProbabilityEntry>();
        }
    }

    internal static class ItemProbabilityService
    {
        private const int MaximumWeight = 1_000_000;
        private const int MaximumEntries = 512;
        private static readonly object SyncRoot = new object();
        private static ItemProbabilityConfiguration current = CreateSafeFallback();

        public static ItemProbabilityConfiguration LoadClientDefaults(
            string gameDirectory,
            out string source)
        {
            try
            {
                string itemPath = Path.GetFullPath(
                    Path.Combine(gameDirectory, "Data", "item.rho"));
                ItemProbabilityConfiguration loaded = LoadFromItemRho(itemPath);
                Validate(loaded, allowEmptyTables: false);
                source = itemPath;
                return loaded;
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is InvalidDataException ||
                exception is NotSupportedException ||
                exception is ArgumentException)
            {
                source = $"내장 안전 기본값 ({exception.Message})";
                return CreateSafeFallback();
            }
        }

        public static void Configure(
            string gameDirectory,
            ItemProbabilityConfiguration configured)
        {
            ItemProbabilityConfiguration selected = configured?.Clone()
                ?? new ItemProbabilityConfiguration();
            bool usesIndividualOverride = selected.Individual.Count > 0;
            bool usesTeamOverride = selected.Team.Count > 0;

            ItemProbabilityConfiguration defaults = null;
            string source = "UI 사용자 설정";
            if (!usesIndividualOverride || !usesTeamOverride)
            {
                defaults = LoadClientDefaults(gameDirectory, out source);
            }

            if (!usesIndividualOverride)
            {
                selected.Individual = ItemProbabilityConfiguration.CloneEntries(
                    defaults.Individual);
            }
            if (!usesTeamOverride)
            {
                selected.Team = ItemProbabilityConfiguration.CloneEntries(defaults.Team);
            }

            Validate(selected, allowEmptyTables: false);
            lock (SyncRoot)
            {
                current = selected;
            }

            string origin = usesIndividualOverride && usesTeamOverride
                ? "UI 사용자 설정"
                : source;
            Console.WriteLine(
                $"[아이템 확률] 원본={origin}; 순위 기준={selected.RankBand}; " +
                $"개인전={selected.Individual.Count}종; 팀전={selected.Team.Count}종");
        }

        public static short NextItem(bool teamMode)
        {
            return NextItem(teamMode, liveRank: -1, racerCount: 0);
        }

        public static short NextItem(bool teamMode, int liveRank, int racerCount)
        {
            ItemProbabilityConfiguration snapshot;
            lock (SyncRoot)
            {
                snapshot = current;
            }

            IReadOnlyList<ItemProbabilityEntry> entries = teamMode
                ? snapshot.Team
                : snapshot.Individual;
            ItemProbabilityRankBand effectiveRankBand = ResolveRankBand(
                snapshot.RankBand,
                liveRank,
                racerCount);
            long totalWeight = 0;
            foreach (ItemProbabilityEntry entry in entries)
            {
                totalWeight += entry.GetWeight(effectiveRankBand);
            }

            // current is initialized with a non-empty safe table, and Configure
            // validates every replacement. Keep a final deterministic guard so a
            // malformed runtime mutation can never tear down a client session.
            if (totalWeight <= 0)
            {
                return 6;
            }

            long roll = Random.Shared.NextInt64(totalWeight);
            foreach (ItemProbabilityEntry entry in entries)
            {
                long weight = entry.GetWeight(effectiveRankBand);
                if (roll < weight)
                {
                    return entry.ItemId;
                }
                roll -= weight;
            }

            return entries.Count > 0 ? entries[entries.Count - 1].ItemId : (short)6;
        }

        internal static ItemProbabilityRankBand ResolveRankBand(
            ItemProbabilityRankBand configuredRankBand,
            int liveRank,
            int racerCount)
        {
            if (configuredRankBand != ItemProbabilityRankBand.Live)
            {
                return configuredRankBand;
            }

            // P5136 sends a zero-based client-computed rank in each item-box
            // GameSlotPacket. Invalid/missing context (for example a starting
            // item) safely falls back to the combined table.
            if (racerCount <= 0 || liveRank < 0 || liveRank >= racerCount)
            {
                return ItemProbabilityRankBand.Combined;
            }
            if (liveRank == 0)
            {
                return ItemProbabilityRankBand.Top;
            }

            // Keep first place in the dedicated toprank table and split every
            // remaining place as evenly as possible across high/mid/low. With
            // eight racers this maps 2-3 / 4-5 / 6-8 place respectively.
            int remainingRacers = racerCount - 1;
            int bucket = ((liveRank * 3) + remainingRacers - 1) /
                         remainingRacers - 1;
            return bucket switch
            {
                <= 0 => ItemProbabilityRankBand.High,
                1 => ItemProbabilityRankBand.Middle,
                _ => ItemProbabilityRankBand.Low
            };
        }

        public static void Validate(
            ItemProbabilityConfiguration configuration,
            bool allowEmptyTables)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }
            if (!Enum.IsDefined(configuration.RankBand))
            {
                throw new InvalidDataException("아이템 확률 순위 기준이 올바르지 않습니다.");
            }

            ValidateEntries(
                configuration.Individual,
                "개인전",
                configuration.RankBand,
                allowEmptyTables);
            ValidateEntries(
                configuration.Team,
                "팀전",
                configuration.RankBand,
                allowEmptyTables);
        }

        private static void ValidateEntries(
            IReadOnlyCollection<ItemProbabilityEntry> entries,
            string label,
            ItemProbabilityRankBand rankBand,
            bool allowEmpty)
        {
            if (entries == null)
            {
                throw new InvalidDataException($"{label} 아이템 확률표가 null입니다.");
            }
            if (entries.Count == 0)
            {
                if (allowEmpty)
                {
                    return;
                }
                throw new InvalidDataException($"{label} 아이템 확률표가 비어 있습니다.");
            }
            if (entries.Count > MaximumEntries)
            {
                throw new InvalidDataException(
                    $"{label} 아이템 확률표는 {MaximumEntries}행을 넘을 수 없습니다.");
            }

            HashSet<short> itemIds = new HashSet<short>();
            long topWeight = 0;
            long highWeight = 0;
            long middleWeight = 0;
            long lowWeight = 0;
            foreach (ItemProbabilityEntry entry in entries)
            {
                if (entry == null || entry.ItemId <= 0 || !itemIds.Add(entry.ItemId))
                {
                    throw new InvalidDataException(
                        $"{label} 아이템 ID가 없거나 중복되었습니다.");
                }
                if (entry.Name != null &&
                    (entry.Name.Length > 64 || entry.Name.Any(char.IsControl)))
                {
                    throw new InvalidDataException(
                        $"{label} 아이템 {entry.ItemId}의 이름이 올바르지 않습니다.");
                }

                ValidateWeight(entry.TopWeight, label, entry.ItemId, "1위");
                ValidateWeight(entry.HighWeight, label, entry.ItemId, "상위");
                ValidateWeight(entry.MiddleWeight, label, entry.ItemId, "중위");
                ValidateWeight(entry.LowWeight, label, entry.ItemId, "하위");
                topWeight += entry.TopWeight;
                highWeight += entry.HighWeight;
                middleWeight += entry.MiddleWeight;
                lowWeight += entry.LowWeight;
            }

            if (rankBand == ItemProbabilityRankBand.Live &&
                (topWeight <= 0 || highWeight <= 0 ||
                 middleWeight <= 0 || lowWeight <= 0))
            {
                throw new InvalidDataException(
                    $"{label} 실시간 순위 확률표는 1위/상위/중위/하위 구간마다 " +
                    "가중치가 하나 이상 필요합니다.");
            }

            long activeWeight = rankBand switch
            {
                ItemProbabilityRankBand.Top => topWeight,
                ItemProbabilityRankBand.High => highWeight,
                ItemProbabilityRankBand.Middle => middleWeight,
                ItemProbabilityRankBand.Low => lowWeight,
                _ => topWeight + highWeight + middleWeight + lowWeight
            };
            if (activeWeight <= 0)
            {
                throw new InvalidDataException(
                    $"{label} 아이템 확률표의 선택된 순위 기준 가중치 합이 0입니다.");
            }
        }

        private static void ValidateWeight(
            int value,
            string label,
            short itemId,
            string rank)
        {
            if (value < 0 || value > MaximumWeight)
            {
                throw new InvalidDataException(
                    $"{label} 아이템 {itemId}의 {rank} 가중치는 0~{MaximumWeight} 범위여야 합니다.");
            }
        }

        private static ItemProbabilityConfiguration LoadFromItemRho(string itemPath)
        {
            if (!File.Exists(itemPath))
            {
                throw new FileNotFoundException("item.rho를 찾을 수 없습니다.", itemPath);
            }

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            using Rho rho = new Rho(itemPath);
            return new ItemProbabilityConfiguration
            {
                Individual = ReadTable(rho, "/slot/itemProb_indi@zz.bml"),
                Team = ReadTable(rho, "/slot/itemProb_team@zz.bml")
            };
        }

        private static List<ItemProbabilityEntry> ReadTable(Rho rho, string path)
        {
            RhoFileInfo file = rho.GetFile(path)
                ?? throw new InvalidDataException($"item.rho에 {path}가 없습니다.");
            BinaryXmlDocument document = new BinaryXmlDocument();
            document.Read(Encoding.Unicode, file.GetData());
            if (!string.Equals(document.RootTag.Name, "items", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"{path}의 루트 태그가 올바르지 않습니다.");
            }

            List<ItemProbabilityEntry> entries = new List<ItemProbabilityEntry>();
            foreach (BinaryXmlTag item in document.RootTag.Children)
            {
                if (!string.Equals(item.Name, "item", StringComparison.OrdinalIgnoreCase) ||
                    !item.Attributes.TryGetValue("idx", out string itemIdText) ||
                    !short.TryParse(itemIdText, out short itemId))
                {
                    continue;
                }

                item.Attributes.TryGetValue("name", out string name);
                entries.Add(new ItemProbabilityEntry
                {
                    ItemId = itemId,
                    Name = name ?? string.Empty,
                    TopWeight = ReadWeight(item, "toprank"),
                    HighWeight = ReadWeight(item, "highrank"),
                    MiddleWeight = ReadWeight(item, "midrank"),
                    LowWeight = ReadWeight(item, "lowrank")
                });
            }
            return entries;
        }

        private static int ReadWeight(BinaryXmlTag item, string attribute)
        {
            return item.Attributes.TryGetValue(attribute, out string value) &&
                   int.TryParse(value, out int parsed)
                ? parsed
                : 0;
        }

        private static ItemProbabilityConfiguration CreateSafeFallback()
        {
            ItemProbabilityEntry[] common =
            {
                Entry(2, "devil"), Entry(3, "ufo"), Entry(4, "waterFly"),
                Entry(5, "magnet"), Entry(6, "booster"), Entry(7, "rocket"),
                Entry(8, "banana"), Entry(9, "waterBomb"), Entry(10, "shield"),
                Entry(11, "angel"), Entry(12, "emp"), Entry(13, "timeBomb"),
                Entry(33, "guideRocket"), Entry(111, "thunderbolt")
            };
            List<ItemProbabilityEntry> individual =
                common.Select(entry => entry.Clone()).ToList();
            List<ItemProbabilityEntry> team =
                common.Select(entry => entry.Clone()).ToList();
            team.Add(Entry(109, "scanning"));
            team.Add(Entry(110, "slotLock"));
            team.Add(Entry(113, "barricade"));
            team.Add(Entry(114, "cloud2"));
            return new ItemProbabilityConfiguration
            {
                RankBand = ItemProbabilityRankBand.Live,
                Individual = individual,
                Team = team
            };
        }

        private static ItemProbabilityEntry Entry(short itemId, string name)
        {
            return new ItemProbabilityEntry
            {
                ItemId = itemId,
                Name = name,
                TopWeight = 1,
                HighWeight = 1,
                MiddleWeight = 1,
                LowWeight = 1
            };
        }
    }
}
