using KartLibrary.Data;
using KartLibrary.File;
using KartLibrary.Xml;
using KartRider.Common.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace KartRider.Compatibility;

public sealed class RandomTrackPoolOverride
{
    public byte GameType { get; set; }

    public uint Selector { get; set; }

    public List<string> TrackIds { get; set; } = new List<string>();

    internal RandomTrackPoolOverride Clone()
    {
        return new RandomTrackPoolOverride
        {
            GameType = GameType,
            Selector = Selector,
            TrackIds = (TrackIds ?? new List<string>()).ToList()
        };
    }
}

public sealed class RandomTrackConfiguration
{
    public List<RandomTrackPoolOverride> Pools { get; set; } =
        new List<RandomTrackPoolOverride>();

    public RandomTrackConfiguration Clone()
    {
        return new RandomTrackConfiguration
        {
            Pools = (Pools ?? new List<RandomTrackPoolOverride>())
                .Select(pool => pool?.Clone())
                .Where(pool => pool != null)
                .ToList()
        };
    }

    public void Validate()
    {
        List<RandomTrackPoolOverride> pools = Pools ?? new List<RandomTrackPoolOverride>();
        if (pools.Count > 32)
        {
            throw new InvalidDataException("랜덤 맵 사용자 지정 목록이 너무 많습니다.");
        }

        var keys = new HashSet<(byte GameType, uint Selector)>();
        foreach (RandomTrackPoolOverride pool in pools)
        {
            if (pool == null ||
                pool.GameType > 1 ||
                !Korean5136RandomTrackService.IsSupportedSelector(pool.Selector))
            {
                throw new InvalidDataException("지원하지 않는 랜덤 맵 목록 설정이 있습니다.");
            }
            if (!keys.Add((pool.GameType, pool.Selector)))
            {
                throw new InvalidDataException("같은 랜덤 맵 목록이 두 번 저장되어 있습니다.");
            }

            List<string> trackIds = pool.TrackIds ?? new List<string>();
            if (trackIds.Count == 0 || trackIds.Count > 512)
            {
                throw new InvalidDataException(
                    "수동 랜덤 맵 목록에는 1개 이상 512개 이하의 트랙이 필요합니다.");
            }

            var uniqueTrackIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string trackId in trackIds)
            {
                if (string.IsNullOrWhiteSpace(trackId) ||
                    trackId.Length > 80 ||
                    trackId.Any(char.IsControl) ||
                    !uniqueTrackIds.Add(trackId))
                {
                    throw new InvalidDataException(
                        "수동 랜덤 맵 목록에 잘못되었거나 중복된 트랙이 있습니다.");
                }
            }
        }
    }

    internal bool TryGetOverride(
        byte gameType,
        uint selector,
        out RandomTrackPoolOverride configuredPool)
    {
        configuredPool = (Pools ?? new List<RandomTrackPoolOverride>())
            .FirstOrDefault(pool =>
                pool != null &&
                pool.GameType == gameType &&
                pool.Selector == selector);
        return configuredPool != null;
    }
}

internal sealed class Korean5136RandomTrackDefinition
{
    public string Id { get; init; } = string.Empty;

    public string KoreanName { get; init; } = string.Empty;

    public string GameType { get; init; } = string.Empty;

    public bool BasicAi { get; init; }

    public uint Hash { get; init; }
}

internal sealed class Korean5136RandomTrackPool
{
    public byte GameType { get; init; }

    public uint Selector { get; init; }

    public string KoreanName { get; init; } = string.Empty;

    public IReadOnlyList<string> DefaultTrackIds { get; init; } = Array.Empty<string>();
}

internal sealed class Korean5136RandomTrackCatalog
{
    private readonly IReadOnlyDictionary<string, Korean5136RandomTrackDefinition> tracksById;
    private readonly IReadOnlyDictionary<(byte GameType, uint Selector), Korean5136RandomTrackPool> poolsByKey;

    public Korean5136RandomTrackCatalog(
        string sourcePath,
        IEnumerable<Korean5136RandomTrackDefinition> tracks,
        IEnumerable<Korean5136RandomTrackPool> pools)
    {
        SourcePath = Path.GetFullPath(sourcePath);
        Tracks = tracks
            .OrderBy(track => track.KoreanName, StringComparer.CurrentCulture)
            .ToArray();
        Pools = pools
            .OrderBy(pool => pool.GameType)
            .ThenBy(pool => pool.Selector)
            .ToArray();
        tracksById = Tracks.ToDictionary(
            track => track.Id,
            StringComparer.OrdinalIgnoreCase);
        poolsByKey = Pools.ToDictionary(pool => (pool.GameType, pool.Selector));
    }

    public string SourcePath { get; }

    public IReadOnlyList<Korean5136RandomTrackDefinition> Tracks { get; }

    public IReadOnlyList<Korean5136RandomTrackPool> Pools { get; }

    public bool TryGetTrack(string id, out Korean5136RandomTrackDefinition track) =>
        tracksById.TryGetValue(id, out track);

    public bool TryGetPool(
        byte gameType,
        uint selector,
        out Korean5136RandomTrackPool pool) =>
        poolsByKey.TryGetValue((gameType, selector), out pool);

    public IReadOnlyList<Korean5136RandomTrackDefinition> GetCompatibleTracks(
        Korean5136RandomTrackPool pool)
    {
        return Tracks
            .Where(track => IsCompatibleTrack(pool, track))
            .OrderBy(track => track.KoreanName, StringComparer.CurrentCulture)
            .ToArray();
    }

    public bool IsCompatibleTrack(
        Korean5136RandomTrackPool pool,
        Korean5136RandomTrackDefinition track)
    {
        bool reverse = track.Id.EndsWith("_rvs", StringComparison.OrdinalIgnoreCase);
        bool crazy = track.Id.EndsWith("_crz", StringComparison.OrdinalIgnoreCase);
        if (pool.Selector == 30 && !reverse ||
            pool.Selector == 23 && !crazy ||
            pool.Selector != 30 && pool.Selector != 23 && (reverse || crazy))
        {
            return false;
        }

        if (pool.GameType == 1)
        {
            return track.GameType.Equals("item", StringComparison.OrdinalIgnoreCase);
        }
        if (pool.Selector == 40)
        {
            return track.GameType.Equals("speed", StringComparison.OrdinalIgnoreCase);
        }
        return track.GameType.Equals("speed", StringComparison.OrdinalIgnoreCase) ||
               track.GameType.Equals("item", StringComparison.OrdinalIgnoreCase);
    }
}

internal static class Korean5136RandomTrackService
{
    private static readonly object SyncRoot = new object();
    private static readonly uint[] SupportedSelectors =
    {
        0, 1, 3, 4, 5, 6, 7, 8, 23, 30, 33, 40
    };

    private static Korean5136RandomTrackCatalog activeCatalog;
    private static Korean5136RandomTrackCatalog catalogOverrideForTesting;
    private static RandomTrackConfiguration activeConfiguration =
        new RandomTrackConfiguration();

    internal static bool IsSupportedSelector(uint selector) =>
        Array.IndexOf(SupportedSelectors, selector) >= 0;

    internal static bool TryLoadClientCatalog(
        string gameDirectory,
        out Korean5136RandomTrackCatalog catalog,
        out string error)
    {
        try
        {
            catalog = LoadClientCatalog(gameDirectory);
            error = string.Empty;
            return true;
        }
        catch (Exception exception)
        {
            catalog = null;
            error = exception.Message;
            return false;
        }
    }

    internal static Korean5136RandomTrackCatalog LoadClientCatalog(string gameDirectory)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        string trackCommonPath = ResolveTrackCommonPath(gameDirectory);
        using Rho rho = new Rho(trackCommonPath);
        BinaryXmlTag randomRoot = ReadDocument(rho, "/randomTrack@kr.bml").RootTag;
        BinaryXmlTag trackRoot = ReadDocument(rho, "/track@zz.bml").RootTag;
        BinaryXmlTag localeRoot = ReadDocument(rho, "/trackLocale@kr.bml").RootTag;

        Dictionary<string, MutableTrack> tracks = BuildTracks(trackRoot, localeRoot);
        Korean5136RandomTrackDefinition[] definitions = tracks.Values
            .Where(track =>
                !track.Blocked &&
                !string.IsNullOrWhiteSpace(track.Name) &&
                !string.IsNullOrWhiteSpace(track.GameType))
            .Select(track => new Korean5136RandomTrackDefinition
            {
                Id = track.Id,
                KoreanName = track.Name,
                GameType = track.GameType,
                BasicAi = track.BasicAi,
                Hash = Adler32Helper.GenerateAdler32_UNICODE(track.Id, 0)
            })
            .ToArray();
        Dictionary<string, Korean5136RandomTrackDefinition> definitionsById =
            definitions.ToDictionary(track => track.Id, StringComparer.OrdinalIgnoreCase);
        Korean5136RandomTrackPool[] pools = BuildPools(randomRoot, definitionsById);
        if (definitions.Length < 200 || pools.Length < 15)
        {
            throw new InvalidDataException(
                $"클라이언트 랜덤 트랙 데이터가 불완전합니다. " +
                $"트랙={definitions.Length}, 목록={pools.Length}");
        }

        return new Korean5136RandomTrackCatalog(trackCommonPath, definitions, pools);
    }

    internal static void Configure(
        string gameDirectory,
        RandomTrackConfiguration configuration)
    {
        Korean5136RandomTrackCatalog catalog;
        lock (SyncRoot)
        {
            catalog = catalogOverrideForTesting;
        }
        catalog ??= LoadClientCatalog(gameDirectory);
        RandomTrackConfiguration snapshot = configuration?.Clone()
            ?? new RandomTrackConfiguration();
        Publish(catalog, snapshot);
    }

    internal static void ConfigureForTesting(
        Korean5136RandomTrackCatalog catalog,
        RandomTrackConfiguration configuration)
    {
        Publish(
            catalog ?? throw new ArgumentNullException(nameof(catalog)),
            configuration?.Clone() ?? new RandomTrackConfiguration());
    }

    internal static void SetCatalogOverrideForTesting(
        Korean5136RandomTrackCatalog catalog)
    {
        lock (SyncRoot)
        {
            catalogOverrideForTesting = catalog;
        }
    }

    private static void Publish(
        Korean5136RandomTrackCatalog catalog,
        RandomTrackConfiguration snapshot)
    {
        snapshot.Validate();
        ValidateAgainstCatalog(snapshot, catalog);

        lock (SyncRoot)
        {
            activeCatalog = catalog;
            activeConfiguration = snapshot;
        }

        global::KartRider.RandomTrack.TrackList = catalog.Tracks.ToDictionary(
            track => track.Hash,
            track => new global::KartRider.Track
            {
                hash = track.Hash,
                ID = track.Id,
                Name = track.KoreanName,
                gameType = track.GameType,
                basicAi = track.BasicAi
            });
        global::KartRider.RandomTrack.ClearAllUsedTracks();
        Console.WriteLine(
            "[랜덤 맵] 클라이언트 목록 로드: 트랙 {0}개, 목록 {1}개, 수동 설정 {2}개, 경로={3}",
            catalog.Tracks.Count,
            catalog.Pools.Count,
            snapshot.Pools.Count,
            catalog.SourcePath);
    }

    internal static bool TryGetCandidateHashes(
        byte gameType,
        uint selector,
        bool basicAiOnly,
        out IReadOnlyList<uint> hashes)
    {
        Korean5136RandomTrackCatalog catalog;
        RandomTrackConfiguration configuration;
        lock (SyncRoot)
        {
            catalog = activeCatalog;
            configuration = activeConfiguration;
        }

        if (catalog == null || !catalog.TryGetPool(gameType, selector, out var pool))
        {
            hashes = Array.Empty<uint>();
            return false;
        }

        IReadOnlyList<string> ids = configuration.TryGetOverride(
            gameType,
            selector,
            out RandomTrackPoolOverride configuredPool)
            ? configuredPool.TrackIds
            : pool.DefaultTrackIds;
        Korean5136RandomTrackDefinition[] selected = ids
            .Select(id => catalog.TryGetTrack(id, out var track) ? track : null)
            .Where(track => track != null)
            .ToArray();
        Korean5136RandomTrackDefinition[] aiSelected = basicAiOnly
            ? selected.Where(track => track.BasicAi).ToArray()
            : selected;
        if (basicAiOnly && aiSelected.Length == 0)
        {
            aiSelected = selected;
        }
        hashes = aiSelected.Select(track => track.Hash).Distinct().ToArray();
        return hashes.Count > 0;
    }

    private static void ValidateAgainstCatalog(
        RandomTrackConfiguration configuration,
        Korean5136RandomTrackCatalog catalog)
    {
        foreach (RandomTrackPoolOverride configuredPool in configuration.Pools)
        {
            if (!catalog.TryGetPool(
                    configuredPool.GameType,
                    configuredPool.Selector,
                    out Korean5136RandomTrackPool pool))
            {
                throw new InvalidDataException("현재 클라이언트에 없는 랜덤 맵 목록 설정입니다.");
            }

            HashSet<string> compatible = catalog.GetCompatibleTracks(pool)
                .Select(track => track.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string invalid = configuredPool.TrackIds.FirstOrDefault(id => !compatible.Contains(id));
            if (invalid != null)
            {
                throw new InvalidDataException(
                    $"'{pool.KoreanName}' 목록에 현재 클라이언트에서 사용할 수 없는 트랙이 있습니다.");
            }
        }
    }

    private static BinaryXmlDocument ReadDocument(Rho rho, string path)
    {
        RhoFileInfo file = rho.GetFile(path)
            ?? throw new InvalidDataException($"track_common.rho에 {path}가 없습니다.");
        BinaryXmlDocument document = new BinaryXmlDocument();
        document.Read(Encoding.Unicode, file.GetData());
        return document;
    }

    private static string ResolveTrackCommonPath(string gameDirectory)
    {
        string fullPath = Path.GetFullPath(gameDirectory ?? string.Empty);
        if (File.Exists(fullPath) &&
            Path.GetFileName(fullPath).Equals(
                "track_common.rho",
                StringComparison.OrdinalIgnoreCase))
        {
            return fullPath;
        }

        if (File.Exists(fullPath) &&
            Path.GetFileName(fullPath).Equals("aaa.pk", StringComparison.OrdinalIgnoreCase))
        {
            fullPath = Path.GetDirectoryName(fullPath)
                ?? throw new InvalidDataException("aaa.pk의 상위 폴더를 확인할 수 없습니다.");
            string adjacentTrackCommon = Path.Combine(fullPath, "track_common.rho");
            if (File.Exists(adjacentTrackCommon))
            {
                return adjacentTrackCommon;
            }
        }

        string path = Path.Combine(fullPath, "Data", "track_common.rho");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "클라이언트 Data\\track_common.rho를 찾을 수 없습니다.",
                path);
        }
        return path;
    }

    private static Dictionary<string, MutableTrack> BuildTracks(
        BinaryXmlTag trackRoot,
        BinaryXmlTag localeRoot)
    {
        var tracks = new Dictionary<string, MutableTrack>(StringComparer.OrdinalIgnoreCase);
        foreach (BinaryXmlTag tag in trackRoot.Children.Where(tag =>
            tag.Name.Equals("track", StringComparison.OrdinalIgnoreCase)))
        {
            string id = Attribute(tag, "id");
            if (string.IsNullOrWhiteSpace(id) || id.Contains("_S", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            tracks[id] = new MutableTrack
            {
                Id = id,
                GameType = Attribute(tag, "gameType"),
                BasicAi = AttributeIsTrue(tag, "basicAi")
            };
        }
        AddTechnicalVariants(tracks, trackRoot, "track_crz", "crz");
        AddTechnicalVariants(tracks, trackRoot, "track_rvs", "rvs");

        foreach (BinaryXmlTag tag in localeRoot.Children.Where(tag =>
            tag.Name.Equals("track", StringComparison.OrdinalIgnoreCase)))
        {
            string id = Attribute(tag, "id");
            if (!tracks.TryGetValue(id, out MutableTrack track))
            {
                continue;
            }
            track.Name = Attribute(tag, "name");
            track.Blocked = AttributeIsTrue(tag, "blocked");
            if (tag.Attributes.ContainsKey("basicAi"))
            {
                track.BasicAi = AttributeIsTrue(tag, "basicAi");
            }
        }
        AddLocaleVariants(tracks, localeRoot, "track_crz", "crz");
        AddLocaleVariants(tracks, localeRoot, "track_rvs", "rvs");
        return tracks;
    }

    private static void AddTechnicalVariants(
        IDictionary<string, MutableTrack> tracks,
        BinaryXmlTag root,
        string elementName,
        string suffix)
    {
        foreach (BinaryXmlTag tag in root.Children.Where(tag =>
            tag.Name.Equals(elementName, StringComparison.OrdinalIgnoreCase)))
        {
            string referenceId = Attribute(tag, "refId");
            if (!tracks.TryGetValue(referenceId, out MutableTrack baseTrack))
            {
                continue;
            }
            string id = $"{referenceId}_{suffix}";
            tracks[id] = new MutableTrack
            {
                Id = id,
                GameType = baseTrack.GameType,
                BasicAi = tag.Attributes.ContainsKey("basicAi")
                    ? AttributeIsTrue(tag, "basicAi")
                    : baseTrack.BasicAi
            };
        }
    }

    private static void AddLocaleVariants(
        IDictionary<string, MutableTrack> tracks,
        BinaryXmlTag root,
        string elementName,
        string suffix)
    {
        foreach (BinaryXmlTag tag in root.Children.Where(tag =>
            tag.Name.Equals(elementName, StringComparison.OrdinalIgnoreCase)))
        {
            string referenceId = Attribute(tag, "refId");
            string id = $"{referenceId}_{suffix}";
            if (!tracks.TryGetValue(referenceId, out MutableTrack baseTrack) ||
                !tracks.TryGetValue(id, out MutableTrack variant))
            {
                continue;
            }

            string explicitName = Attribute(tag, "name");
            variant.Name = !string.IsNullOrWhiteSpace(explicitName)
                ? explicitName
                : suffix.Equals("rvs", StringComparison.OrdinalIgnoreCase)
                    ? $"[리버스] {baseTrack.Name}"
                    : $"[크레이지] {baseTrack.Name}";
            variant.Blocked = baseTrack.Blocked || AttributeIsTrue(tag, "blocked");
            if (tag.Attributes.ContainsKey("basicAi"))
            {
                variant.BasicAi = AttributeIsTrue(tag, "basicAi");
            }
        }
    }

    private static Korean5136RandomTrackPool[] BuildPools(
        BinaryXmlTag randomRoot,
        IReadOnlyDictionary<string, Korean5136RandomTrackDefinition> tracks)
    {
        var sets = randomRoot.Children
            .Where(tag => tag.Name.Equals("RandomTrackSet", StringComparison.OrdinalIgnoreCase))
            .GroupBy(tag => (
                GameType: Attribute(tag, "gameType"),
                RandomType: Attribute(tag, "randomType")))
            .ToDictionary(
                group => group.Key,
                group => group.SelectMany(TrackIds).Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        var lists = randomRoot.Children
            .Where(tag => tag.Name.Equals("RandomTrackList", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                tag => Attribute(tag, "randomType"),
                tag => TrackIds(tag).ToArray(),
                StringComparer.OrdinalIgnoreCase);
        var pools = new List<Korean5136RandomTrackPool>();

        IEnumerable<string> Active(IEnumerable<string> ids) => ids
            .Where(id => tracks.ContainsKey(id))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        IEnumerable<string> BaseTracks(params string[] gameTypes) => tracks.Values
            .Where(track =>
                gameTypes.Contains(track.GameType, StringComparer.OrdinalIgnoreCase) &&
                !track.Id.EndsWith("_rvs", StringComparison.OrdinalIgnoreCase) &&
                !track.Id.EndsWith("_crz", StringComparison.OrdinalIgnoreCase))
            .Select(track => track.Id);
        IEnumerable<string> Direct(string gameType, string randomType) =>
            sets.TryGetValue((gameType, randomType), out string[] ids)
                ? Active(ids)
                : Array.Empty<string>();
        IEnumerable<string> Listed(string randomType) =>
            lists.TryGetValue(randomType, out string[] ids)
                ? Active(ids)
                : Array.Empty<string>();
        void Add(byte gameType, uint selector, IEnumerable<string> ids)
        {
            string[] normalized = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (normalized.Length == 0)
            {
                return;
            }
            pools.Add(new Korean5136RandomTrackPool
            {
                GameType = gameType,
                Selector = selector,
                KoreanName = GetPoolDisplayName(gameType, selector),
                DefaultTrackIds = normalized
            });
        }

        Add(0, 0, BaseTracks("speed", "item"));
        Add(1, 0, BaseTracks("item"));
        Add(0, 1, Direct("speed", "clubSpeed"));
        Add(1, 1, Direct("item", "clubItem"));
        for (uint selector = 3; selector <= 7; selector++)
        {
            string randomType = $"hot{selector - 2}";
            Add(0, selector, Direct("speed", randomType));
            Add(1, selector, Direct("item", randomType));
        }
        string[] newTracks = Listed("new").ToArray();
        Add(0, 8, newTracks);
        Add(1, 8, newTracks.Where(id =>
            tracks[id].GameType.Equals("item", StringComparison.OrdinalIgnoreCase)));
        Add(1, 23, Direct("item", "crazy"));
        string[] reverseTracks = Listed("reverse").ToArray();
        Add(0, 30, reverseTracks.Where(id =>
            tracks[id].GameType.Equals("speed", StringComparison.OrdinalIgnoreCase) ||
            tracks[id].GameType.Equals("item", StringComparison.OrdinalIgnoreCase)));
        Add(1, 30, reverseTracks.Where(id =>
            tracks[id].GameType.Equals("item", StringComparison.OrdinalIgnoreCase)));
        Add(0, 33, Direct("speed", "newLeagueRandom"));
        Add(1, 33, Direct("item", "newLeagueRandom"));
        Add(0, 40, BaseTracks("speed"));
        return pools.ToArray();
    }

    private static IEnumerable<string> TrackIds(BinaryXmlTag tag)
    {
        return tag.Children
            .Where(child => child.Name.Equals("track", StringComparison.OrdinalIgnoreCase))
            .Select(child => Attribute(child, "id"))
            .Where(id => !string.IsNullOrWhiteSpace(id));
    }

    private static string GetPoolDisplayName(byte gameType, uint selector)
    {
        string mode = gameType == 0 ? "스피드전" : "아이템전";
        string name = selector switch
        {
            0 => "전체 랜덤",
            1 => gameType == 0 ? "클럽 스피드 랜덤" : "클럽 아이템 랜덤",
            3 => "인기 랜덤 · 매우 쉬움",
            4 => "인기 랜덤 · 쉬움",
            5 => "인기 랜덤 · 보통",
            6 => "인기 랜덤 · 어려움",
            7 => "인기 랜덤 · 매우 어려움",
            8 => "신규 트랙 랜덤",
            23 => "크레이지 랜덤",
            30 => "리버스 랜덤",
            33 => "새 리그 랜덤",
            40 => "스피드 트랙 전용 랜덤",
            _ => "랜덤"
        };
        return $"{mode} · {name}";
    }

    private static string Attribute(BinaryXmlTag tag, string name) =>
        tag.Attributes.TryGetValue(name, out string value) ? value : string.Empty;

    private static bool AttributeIsTrue(BinaryXmlTag tag, string name) =>
        Attribute(tag, name).Equals("true", StringComparison.OrdinalIgnoreCase);

    private sealed class MutableTrack
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GameType { get; init; } = string.Empty;
        public bool BasicAi { get; set; }
        public bool Blocked { get; set; }
    }
}
