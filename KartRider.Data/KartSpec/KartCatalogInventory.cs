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
}
