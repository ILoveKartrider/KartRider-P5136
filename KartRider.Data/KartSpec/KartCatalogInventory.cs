using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace KartRider;

internal sealed class KartCatalogInventoryItem
{
    public ushort Category { get; init; }
    public ushort Id { get; init; }
    public ushort Serial { get; init; }
    public string Name { get; init; } = string.Empty;
}

internal static class KartCatalogInventory
{
    private static readonly HashSet<ushort> GrantCategoryIds = new HashSet<ushort>
    {
        1, 2, 3, 4, 7, 8, 9, 11, 12, 13, 14, 16, 18, 20, 21, 22, 23,
        26, 27, 28, 30, 31, 32, 36, 37, 38, 39, 43, 44, 45, 46, 49, 52,
        53, 55, 59, 61, 67, 68, 69, 70
    };

    // The KR 5136 shop table contains character rows that are not selectable
    // rider models.  Most are explicitly named as dummy/paper assets, while
    // four licensed rows are likewise absent from the client's grant roster.
    // Advertising ownership of any of these rows makes the character-list UI
    // instantiate an unsupported model and terminate the client.
    private static readonly HashSet<ushort> UnsafeCharacterItemIds = new HashSet<ushort>
    {
        45, 47, 48, 52, 59, 116, 117, 124, 128, 130, 137, 144, 147,
        149, 159, 175, 176, 184, 192, 193, 194, 195, 196, 197, 231,
        245, 246, 247, 265, 301, 302, 333, 350, 376, 377, 391, 392,
        396, 397
    };

    private sealed class Snapshot
    {
        public static readonly Snapshot Empty = new Snapshot(
            Array.Empty<KartCatalogInventoryItem>());

        public Snapshot(IEnumerable<KartCatalogInventoryItem> items)
        {
            KartCatalogInventoryItem[] normalized = (items
                    ?? Enumerable.Empty<KartCatalogInventoryItem>())
                .Where(item => item != null && item.Id != 0)
                .Select(item => new KartCatalogInventoryItem
                {
                    Category = item.Category,
                    Id = item.Id,
                    Serial = item.Serial,
                    Name = item.Name ?? string.Empty
                })
                .GroupBy(item => (item.Category, item.Id))
                .Select(group => group.First())
                .OrderBy(item => item.Category)
                .ThenBy(item => item.Id)
                .ToArray();

            Items = Array.AsReadOnly(normalized);
            CategoryCount = normalized
                .Select(item => item.Category)
                .Distinct()
                .Count();
        }

        public IReadOnlyList<KartCatalogInventoryItem> Items { get; }
        public int CategoryCount { get; }
    }

    private static Snapshot current = Snapshot.Empty;

    public static int TotalItemCount => Volatile.Read(ref current).Items.Count;
    public static int CategoryCount => Volatile.Read(ref current).CategoryCount;

    internal static void Publish(IEnumerable<KartCatalogInventoryItem> items)
    {
        Volatile.Write(ref current, new Snapshot(items));
    }

    internal static IReadOnlyList<KartCatalogInventoryItem> GetItemsSnapshot()
    {
        return Volatile.Read(ref current).Items;
    }

    internal static bool IsGrantCategory(ushort category)
    {
        return GrantCategoryIds.Contains(category);
    }

    internal static bool IsGrantItem(KartCatalogInventoryItem item)
    {
        return item != null &&
            GrantCategoryIds.Contains(item.Category) &&
            (item.Category != 1 || !UnsafeCharacterItemIds.Contains(item.Id));
    }
}
