using ExcData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace KartRider.Compatibility;

[Flags]
public enum Korean5136PlantGameMode : byte
{
    Unknown = 0,
    Speed = 1,
    Item = 2,
    Battle = 4,
    TimeAttack = 8,
    All = Speed | Item | Battle | TimeAttack
}

/// <summary>
/// Korean P5136 plant-part physics snapshot.  Values were recovered from
/// zeta_/kr/enchant/enchantMaterials.xml in the verified P5136 client whose
/// unpacked executable SHA-256 is
/// FD9444C057090C3BB524AF03BFF5EC995620FBB951B9A823D2CD4E9B0494956F.
/// Runtime behavior intentionally does not read or distribute either source.
/// Cosmetic EnchanterSetSpec and item-ability effects are outside this table.
/// </summary>
public static class Korean5136PlantPerformance
{
    private sealed class Spec
    {
        public Korean5136PlantGameMode Modes { get; init; } = Korean5136PlantGameMode.All;
        public float TransAccelFactor { get; init; }
        public float DragFactor { get; init; }
        public float StartForwardAccelSpeed { get; init; }
        public float StartForwardAccelItem { get; init; }
        public float ForwardAccel { get; init; }
        public float StartBoosterTimeSpeed { get; init; }
        public float StartBoosterTimeItem { get; init; }
        public float SlipBrake { get; init; }
        public float GripBrake { get; init; }
        public float RearGripFactor { get; init; }
        public float FrontGripFactor { get; init; }
        public float CornerDrawFactor { get; init; }
        public float SteerConstraint { get; init; }
        public float DriftEscapeForce { get; init; }
        public float DriftMaxGauge { get; init; }
        public float AnimalBoosterTime { get; init; }
        public float AntiCollideBalance { get; init; }
        public float NormalBoosterTime { get; init; }
        public float DriftSlipFactor { get; init; }
        public float TeamBoosterTime { get; init; }
        public byte ItemSlotCapacity { get; init; }
        public byte SpeedSlotCapacity { get; init; }
    }

    private const Korean5136PlantGameMode Speed =
        Korean5136PlantGameMode.Speed | Korean5136PlantGameMode.TimeAttack;
    private const Korean5136PlantGameMode Item =
        Korean5136PlantGameMode.Item | Korean5136PlantGameMode.Battle;

    private static readonly IReadOnlyDictionary<(short Category, short Id), Spec> Specs =
        CreateSpecs();

    public static int EntryCount => Specs.Count;

    public static int GetCategoryEntryCount(short category) =>
        Specs.Keys.Count(key => key.Category == category);

    public static Korean5136PlantGameMode FromRoomGameType(byte gameType) => gameType switch
    {
        1 or 3 => Korean5136PlantGameMode.Speed,
        2 or 4 => Korean5136PlantGameMode.Item,
        _ => Korean5136PlantGameMode.Unknown
    };

    /// <summary>
    /// Applies a known part.  A true result means the category/id belongs to
    /// the P5136 table even when its effect is cosmetic, ability-only, or not
    /// active in the selected game mode.
    /// </summary>
    public static bool Apply(
        ExcSpecs target,
        short category,
        short id,
        Korean5136PlantGameMode mode)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!Specs.TryGetValue((category, id), out Spec spec))
        {
            return false;
        }
        if (spec.Modes != Korean5136PlantGameMode.All &&
            (spec.Modes & mode) == 0)
        {
            return true;
        }

        switch (category)
        {
            case 43:
                target.Plant43_TransAccelFactor = spec.TransAccelFactor;
                target.Plant43_DragFactor = spec.DragFactor;
                target.Plant43_StartForwardAccelSpeed = spec.StartForwardAccelSpeed;
                target.Plant43_StartForwardAccelItem = spec.StartForwardAccelItem;
                target.Plant43_ForwardAccel = spec.ForwardAccel;
                target.Plant43_StartBoosterTimeSpeed = spec.StartBoosterTimeSpeed;
                break;
            case 44:
                target.Plant44_SlipBrake = spec.SlipBrake;
                target.Plant44_GripBrake = spec.GripBrake;
                target.Plant44_RearGripFactor = spec.RearGripFactor;
                target.Plant44_FrontGripFactor = spec.FrontGripFactor;
                target.Plant44_CornerDrawFactor = spec.CornerDrawFactor;
                target.Plant44_SteerConstraint = spec.SteerConstraint;
                break;
            case 45:
                target.Plant45_DriftEscapeForce = spec.DriftEscapeForce;
                target.Plant45_DriftMaxGauge = spec.DriftMaxGauge;
                target.Plant45_CornerDrawFactor = spec.CornerDrawFactor;
                target.Plant45_SlipBrake = spec.SlipBrake;
                target.Plant45_AnimalBoosterTime = spec.AnimalBoosterTime;
                target.Plant45_AntiCollideBalance = spec.AntiCollideBalance;
                target.Plant45_DragFactor = spec.DragFactor;
                break;
            case 46:
                target.Plant46_DriftMaxGauge = spec.DriftMaxGauge;
                target.Plant46_NormalBoosterTime = spec.NormalBoosterTime;
                target.Plant46_DriftSlipFactor = spec.DriftSlipFactor;
                target.Plant46_ForwardAccel = spec.ForwardAccel;
                target.Plant46_AnimalBoosterTime = spec.AnimalBoosterTime;
                target.Plant46_TeamBoosterTime = spec.TeamBoosterTime;
                target.Plant46_StartForwardAccelSpeed = spec.StartForwardAccelSpeed;
                target.Plant46_StartForwardAccelItem = spec.StartForwardAccelItem;
                target.Plant46_StartBoosterTimeSpeed = spec.StartBoosterTimeSpeed;
                target.Plant46_StartBoosterTimeItem = spec.StartBoosterTimeItem;
                target.Plant46_ItemSlotCapacity = spec.ItemSlotCapacity;
                target.Plant46_SpeedSlotCapacity = spec.SpeedSlotCapacity;
                target.Plant46_GripBrake = spec.GripBrake;
                target.Plant46_SlipBrake = spec.SlipBrake;
                break;
        }
        return true;
    }

    private static IReadOnlyDictionary<(short Category, short Id), Spec> CreateSpecs()
    {
        var result = new Dictionary<(short Category, short Id), Spec>();
        void Add(short category, short id, Spec spec) => result.Add((category, id), spec);

        Add(43, 1, new() { TransAccelFactor = 0.002f, DragFactor = -0.0007f, StartForwardAccelSpeed = 0.02f });
        Add(43, 2, new() { TransAccelFactor = 0.002f, ForwardAccel = 2f });
        Add(43, 3, new() { StartForwardAccelSpeed = 0.02f, StartBoosterTimeSpeed = 15f });
        Add(43, 4, new() { StartForwardAccelSpeed = 0.04f });
        Add(43, 5, new() { StartForwardAccelItem = 0.04f });
        Add(43, 6, new() { Modes = Speed, DragFactor = -0.0021f });
        Add(43, 7, new() { DragFactor = -0.0014f });
        Add(43, 8, new() { Modes = Speed, ForwardAccel = 1f, StartForwardAccelSpeed = 0.02f });
        Add(43, 9, new() { ForwardAccel = 1f, StartForwardAccelSpeed = 0.02f });
        Add(43, 10, new() { Modes = Speed, ForwardAccel = 2f });
        Add(43, 11, new() { ForwardAccel = 2f });
        Add(43, 12, new() { DragFactor = -0.0007f, ForwardAccel = 1f });
        Add(43, 13, new() { Modes = Speed, DragFactor = -0.0007f, ForwardAccel = 1f });
        Add(43, 14, new() { DragFactor = -0.0007f });
        Add(43, 15, new() { Modes = Speed, DragFactor = -0.0014f });
        Add(43, 16, new() { TransAccelFactor = 0.0002f, DragFactor = -0.0014f });
        Add(43, 17, new() { TransAccelFactor = 0.0004f, DragFactor = -0.0007f });
        Add(43, 18, new() { TransAccelFactor = 0.0002f, ForwardAccel = 2f });
        Add(43, 19, new() { TransAccelFactor = 0.0004f, ForwardAccel = 1f });
        Add(43, 20, new() { TransAccelFactor = 0.0006f, ForwardAccel = 1f });
        Add(43, 21, new() { TransAccelFactor = 0.0008f });
        Add(43, 22, new() { TransAccelFactor = 0.0012f, DragFactor = -0.0014f });
        Add(43, 23, new() { ForwardAccel = 1f, TransAccelFactor = 0.002f, StartBoosterTimeSpeed = 30f });

        Add(44, 1, new() { SlipBrake = -40f, GripBrake = -40f, RearGripFactor = 0.2f, FrontGripFactor = 0.2f, CornerDrawFactor = 0.0005f });
        Add(44, 2, new() { Modes = Speed, GripBrake = -12f, RearGripFactor = 0.3f, FrontGripFactor = 0.3f, CornerDrawFactor = 0.001f });
        Add(44, 3, new() { SlipBrake = -10f, RearGripFactor = 0.2f, FrontGripFactor = 0.2f });
        Add(44, 4, new() { RearGripFactor = 0.1f, FrontGripFactor = 0.1f });
        Add(44, 5, new() { RearGripFactor = 0.05f, FrontGripFactor = 0.05f, GripBrake = -20f });
        Add(44, 6, new() { GripBrake = -20f });
        Add(44, 7, new() { GripBrake = -15f });
        Add(44, 8, new() { SteerConstraint = 0.2f });
        Add(44, 9, new() { SteerConstraint = 0.4f });
        Add(44, 10, new() { SteerConstraint = 0.8f });
        Add(44, 11, new() { SteerConstraint = -0.4f });
        Add(44, 12, new() { GripBrake = -5f, SlipBrake = -8f });
        Add(44, 13, new() { GripBrake = -7f, SlipBrake = -6f });
        Add(44, 14, new() { GripBrake = -9f, SlipBrake = -4f });
        Add(44, 15, new() { GripBrake = -11f, SlipBrake = -2f });

        Add(45, 1, new() { DriftEscapeForce = 70f, DriftMaxGauge = -40f, CornerDrawFactor = 0.001f });
        Add(45, 2, new() { DriftMaxGauge = -60f, SlipBrake = -192f });
        Add(45, 3, new() { AnimalBoosterTime = 100f, DriftEscapeForce = 70f });
        Add(45, 4, new() { DriftMaxGauge = -60f });
        Add(45, 5, new() { DriftMaxGauge = -40f, AnimalBoosterTime = 100f });
        Add(45, 6, new() { DriftEscapeForce = 50f });
        Add(45, 7, new() { DriftEscapeForce = 30f, CornerDrawFactor = 0.0005f });
        Add(45, 8, new() { DriftMaxGauge = -40f });
        Add(45, 9, new() { DriftMaxGauge = -60f, DriftEscapeForce = -20f });
        Add(45, 10, new() { DriftMaxGauge = -100f, DriftEscapeForce = -60f });
        Add(45, 11, new() { DriftMaxGauge = -80f, DriftEscapeForce = -40f });
        Add(45, 12, new() { DriftEscapeForce = 10f });
        Add(45, 13, new() { DriftEscapeForce = 30f });
        Add(45, 14, new() { Modes = Item, DriftEscapeForce = 50f, DriftMaxGauge = 40f });
        Add(45, 15, new() { DriftEscapeForce = 70f, DriftMaxGauge = 60f });
        Add(45, 16, new() { AntiCollideBalance = -0.005f, CornerDrawFactor = 0.0005f });
        Add(45, 17, new() { AntiCollideBalance = -0.005f, DragFactor = -0.0007f });
        Add(45, 18, new() { AntiCollideBalance = -0.005f, DriftMaxGauge = -40f });
        Add(45, 19, new() { AntiCollideBalance = -0.01f });
        Add(45, 20, new() { AntiCollideBalance = -0.01f, DriftMaxGauge = -30f });
        Add(45, 21, new() { AntiCollideBalance = -0.015f });
        Add(45, 22, new() { AntiCollideBalance = -0.02f, DriftEscapeForce = 30f });
        Add(45, 23, new() { DriftEscapeForce = 90f, CornerDrawFactor = 0.0005f });

        Add(46, 1, new() { Modes = Speed, DriftMaxGauge = -80f, NormalBoosterTime = 120f });
        Add(46, 2, new() { DriftSlipFactor = -0.03f, ForwardAccel = 2f });
        Add(46, 3, new() { AnimalBoosterTime = 200f });
        Add(46, 4, new()); // ability-only: banana -> waterMine
        Add(46, 5, new() { NormalBoosterTime = 90f, TeamBoosterTime = 60f, AnimalBoosterTime = 50f, StartForwardAccelSpeed = 0.02f, StartForwardAccelItem = 0.02f });
        Add(46, 6, new() { NormalBoosterTime = 60f, AnimalBoosterTime = 80f });
        Add(46, 7, new() { StartBoosterTimeSpeed = 105f });
        Add(46, 8, new() { StartBoosterTimeItem = 105f });
        Add(46, 9, new() { StartBoosterTimeSpeed = 195f });
        Add(46, 10, new()); // ability-only: water-bomb shields
        Add(46, 11, new() { Modes = Korean5136PlantGameMode.Battle, ItemSlotCapacity = 3 });
        Add(46, 12, new() { Modes = Korean5136PlantGameMode.TimeAttack, SpeedSlotCapacity = 3 });
        Add(46, 13, new()); // cosmetic lamp color
        Add(46, 14, new()); // cosmetic lamp color
        Add(46, 15, new() { AnimalBoosterTime = 100f, GripBrake = 10f });
        Add(46, 16, new() { AnimalBoosterTime = 100f, SlipBrake = 10f });
        Add(46, 17, new() { Modes = Korean5136PlantGameMode.Battle, AnimalBoosterTime = 100f, SlipBrake = 9f });
        Add(46, 18, new() { Modes = Korean5136PlantGameMode.Battle, AnimalBoosterTime = 120f });
        Add(46, 19, new()); // ability-only: water-fly shield
        Add(46, 20, new()); // ability-only: water-bomb shields
        Add(46, 21, new() { StartBoosterTimeSpeed = 150f });
        Add(46, 22, new() { ForwardAccel = 1.5f });
        Add(46, 23, new() { NormalBoosterTime = 60f });
        Add(46, 24, new() { TeamBoosterTime = 60f });
        Add(46, 25, new() { NormalBoosterTime = 90f, TeamBoosterTime = -30f });
        Add(46, 26, new() { NormalBoosterTime = -30f, TeamBoosterTime = 90f });
        Add(46, 27, new());
        Add(46, 28, new());
        Add(46, 29, new());
        Add(46, 30, new());

        return result;
    }
}
