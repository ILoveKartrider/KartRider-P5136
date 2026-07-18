using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KartRider;

internal enum KartCatalogAbilityKind
{
    TransformByKart,
    FiringToGain,
    FiredToGain
}

internal sealed class KartCatalogItemSymbol
{
    public string Name { get; init; } = string.Empty;
    public short ItemId { get; init; }
    public string Evidence { get; init; } = string.Empty;
}

internal sealed class KartCatalogAbilityRule
{
    public KartCatalogAbilityKind Kind { get; init; }
    public ushort KartId { get; init; }
    public string SourceSymbol { get; init; } = string.Empty;
    public short? SourceItemId { get; init; }
    public string TargetSymbol { get; init; } = string.Empty;
    public short? TargetItemId { get; init; }
    public string ProbabilityText { get; init; } = string.Empty;
    public byte? Probability { get; init; }
    public string Mode { get; init; } = string.Empty;
    public int Step { get; init; }
    public IReadOnlyDictionary<string, string> RawAttributes { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public bool IsRuntimeResolved =>
        SourceItemId.HasValue && TargetItemId.HasValue && Probability.HasValue;
}

internal static class KartCatalogAbilities
{
    private sealed class Snapshot
    {
        public static readonly Snapshot Empty = new Snapshot(Array.Empty<KartCatalogAbilityRule>());

        public Snapshot(IEnumerable<KartCatalogAbilityRule> rules)
        {
            KartCatalogAbilityRule[] allRules = rules?.ToArray()
                ?? Array.Empty<KartCatalogAbilityRule>();
            TotalRuleCount = allRules.Length;
            KartCatalogAbilityRule[] resolved = allRules
                .Where(rule => rule.IsRuntimeResolved)
                .ToArray();
            ResolvedRuleCount = resolved.Length;
            Transform = Group(resolved, KartCatalogAbilityKind.TransformByKart);
            Firing = Group(resolved, KartCatalogAbilityKind.FiringToGain);
            Fired = Group(resolved, KartCatalogAbilityKind.FiredToGain);
        }

        public int TotalRuleCount { get; }
        public int ResolvedRuleCount { get; }
        public IReadOnlyDictionary<(ushort KartId, short SourceItemId), KartCatalogAbilityRule[]> Transform { get; }
        public IReadOnlyDictionary<(ushort KartId, short SourceItemId), KartCatalogAbilityRule[]> Firing { get; }
        public IReadOnlyDictionary<(ushort KartId, short SourceItemId), KartCatalogAbilityRule[]> Fired { get; }

        private static IReadOnlyDictionary<(ushort, short), KartCatalogAbilityRule[]> Group(
            IEnumerable<KartCatalogAbilityRule> rules,
            KartCatalogAbilityKind kind)
        {
            return rules
                .Where(rule => rule.Kind == kind)
                .GroupBy(rule => (rule.KartId, rule.SourceItemId!.Value))
                .ToDictionary(group => group.Key, group => group.ToArray());
        }
    }

    private static Snapshot current = Snapshot.Empty;

    public static int TotalRuleCount => Volatile.Read(ref current).TotalRuleCount;
    public static int ResolvedRuleCount => Volatile.Read(ref current).ResolvedRuleCount;

    internal static void Publish(IEnumerable<KartCatalogAbilityRule> rules)
    {
        Volatile.Write(ref current, new Snapshot(rules));
    }

    internal static bool TryGetTransform(
        ushort kartId,
        short sourceItemId,
        string acquisitionType,
        out KartCatalogAbilityRule rule)
    {
        Snapshot snapshot = Volatile.Read(ref current);
        if (!snapshot.Transform.TryGetValue((kartId, sourceItemId), out var candidates))
        {
            rule = null;
            return false;
        }

        rule = SelectByMode(candidates, acquisitionType, requiredStep: null);
        return rule != null;
    }

    internal static bool HasTransformDefinition(ushort kartId, short sourceItemId)
    {
        Snapshot snapshot = Volatile.Read(ref current);
        return snapshot.Transform.ContainsKey((kartId, sourceItemId));
    }

    internal static bool TryGetFiringToGain(
        ushort kartId,
        short sourceItemId,
        int firingStep,
        string gameType,
        out KartCatalogAbilityRule rule)
    {
        Snapshot snapshot = Volatile.Read(ref current);
        if (!snapshot.Firing.TryGetValue((kartId, sourceItemId), out var candidates))
        {
            rule = null;
            return false;
        }

        rule = SelectByMode(candidates, gameType, firingStep);
        return rule != null;
    }

    internal static bool TryGetFiredToGain(
        ushort kartId,
        short sourceItemId,
        string gameType,
        out KartCatalogAbilityRule rule)
    {
        Snapshot snapshot = Volatile.Read(ref current);
        if (!snapshot.Fired.TryGetValue((kartId, sourceItemId), out var candidates))
        {
            rule = null;
            return false;
        }

        rule = SelectByMode(candidates, gameType, requiredStep: null);
        return rule != null;
    }

    private static KartCatalogAbilityRule SelectByMode(
        IEnumerable<KartCatalogAbilityRule> candidates,
        string requestedMode,
        int? requiredStep)
    {
        string mode = requestedMode ?? string.Empty;
        KartCatalogAbilityRule[] stepMatches = candidates
            .Where(rule => !requiredStep.HasValue || rule.Step == requiredStep.Value)
            .ToArray();
        KartCatalogAbilityRule exact = stepMatches.FirstOrDefault(rule =>
            !string.IsNullOrWhiteSpace(mode) &&
            rule.Mode.Equals(mode, StringComparison.OrdinalIgnoreCase));
        return exact ?? stepMatches.FirstOrDefault(rule => string.IsNullOrWhiteSpace(rule.Mode));
    }
}
