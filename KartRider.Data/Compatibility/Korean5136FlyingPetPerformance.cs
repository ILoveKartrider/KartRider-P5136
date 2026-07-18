using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace KartRider.Compatibility;

/// <summary>
/// Immutable Korean P5136 flying-pet physics snapshot. The values are kept in
/// the build profile so normal server operation does not depend on a secondary
/// XML file or on retaining client archives.
/// </summary>
internal static class Korean5136FlyingPetPerformance
{
    internal const int ExpectedEntryCount = 80;

    private static readonly FrozenDictionary<ushort, Spec> Specs = CreateSpecs();

    internal static int EntryCount => Specs.Count;

    internal static bool TryGet(ushort id, out Spec spec) =>
        Specs.TryGetValue(id, out spec);

    private static FrozenDictionary<ushort, Spec> CreateSpecs()
    {
        var specs = new Dictionary<ushort, Spec>
        {
            [1] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [2] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [3] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [4] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [5] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [6] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [7] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [8] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [9] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [10] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [11] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [12] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [13] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [14] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [15] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [16] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [17] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [18] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [19] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 560.0f, 560.0f),
            [25] = new(0.0f, 3.5f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [26] = new(0.0f, 0.0f, 100.0f, 0.002f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [27] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [28] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f),
            [29] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [30] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [31] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 800.0f, 800.0f),
            [32] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [33] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [34] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 800.0f, 800.0f),
            [35] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [36] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [37] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [38] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [39] = new(0.0f, 0.0f, 155.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [40] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 800.0f, 800.0f),
            [41] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 800.0f, 800.0f),
            [42] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f),
            [43] = new(0.0f, 0.0f, 0.0f, 0.002f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [44] = new(0.0f, 3.5f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f),
            [45] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [46] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1000.0f, 1000.0f),
            [47] = new(0.0f, 5.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [48] = new(0.0f, 0.0f, 100.0f, 0.002f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [49] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [50] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 800.0f, 800.0f),
            [51] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [53] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1000.0f, 1000.0f),
            [54] = new(0.0f, 5.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [56] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [57] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 800.0f, 800.0f),
            [58] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f),
            [59] = new(0.0f, 5.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [60] = new(0.0f, 3.5f, 0.0f, 0.0f, 0.0f, 0.0f, 300.0f, 0.0f, 0.0f),
            [61] = new(0.0f, 5.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [62] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [63] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 300.0f, 0.0f, 1000.0f, 1000.0f),
            [64] = new(0.0f, 3.5f, 0.0f, 0.002f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f),
            [65] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 800.0f, 800.0f),
            [66] = new(0.0f, 5.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [67] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [68] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f),
            [69] = new(0.0f, 5.0f, 0.0f, 0.0f, 0.0f, 300.0f, 0.0f, 0.0f, 0.0f),
            [71] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 300.0f, 1000.0f, 1000.0f),
            [72] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1000.0f, 1000.0f),
            [73] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [74] = new(0.0f, 0.0f, 0.0f, 0.002f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [75] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 300.0f, 0.0f, 1300.0f, 0.0f),
            [76] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 800.0f, 800.0f),
            [77] = new(0.0f, 5.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [78] = new(0.0f, 5.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [79] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [80] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 800.0f, 800.0f),
            [81] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f),
            [82] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [83] = new(0.0f, 3.5f, 0.0f, 0.0f, 0.0f, 300.0f, 0.0f, 0.0f, 0.0f),
            [84] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 0.0f, 0.0f, 800.0f, 800.0f),
            [85] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 250.0f, 800.0f, 800.0f),
            [86] = new(0.0f, 0.0f, 100.0f, 0.0f, 0.0f, 250.0f, 0.0f, 0.0f, 0.0f),
            [87] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 1300.0f, 1300.0f),
            [88] = new(0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 300.0f, 1000.0f, 1000.0f),
        };

        if (specs.Count != ExpectedEntryCount ||
            !specs.TryGetValue(32, out Spec id32) ||
            id32.StartForwardAccelForceItem != 1300.0f ||
            id32.StartForwardAccelForceSpeed != 1300.0f ||
            !specs.TryGetValue(83, out Spec id83) ||
            id83.ForwardAccelForce != 3.5f ||
            id83.ItemBoosterTime != 300.0f ||
            specs.Values.Any(spec => !spec.IsFinite))
        {
            throw new InvalidOperationException(
                "The Korean P5136 flying-pet performance table is incomplete or invalid.");
        }

        return specs.ToFrozenDictionary();
    }

    internal readonly record struct Spec(
        float DragFactor,
        float ForwardAccelForce,
        float DriftEscapeForce,
        float CornerDrawFactor,
        float NormalBoosterTime,
        float ItemBoosterTime,
        float TeamBoosterTime,
        float StartForwardAccelForceItem,
        float StartForwardAccelForceSpeed)
    {
        internal bool IsFinite =>
            float.IsFinite(DragFactor) &&
            float.IsFinite(ForwardAccelForce) &&
            float.IsFinite(DriftEscapeForce) &&
            float.IsFinite(CornerDrawFactor) &&
            float.IsFinite(NormalBoosterTime) &&
            float.IsFinite(ItemBoosterTime) &&
            float.IsFinite(TeamBoosterTime) &&
            float.IsFinite(StartForwardAccelForceItem) &&
            float.IsFinite(StartForwardAccelForceSpeed);
    }
}
