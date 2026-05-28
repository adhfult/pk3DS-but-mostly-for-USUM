using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace pk3DS.Core.Structures;

[StructLayout(LayoutKind.Sequential)]
public struct Item
{
    public Item(byte[] data) => this = data.ToStructure<Item>();
    public readonly byte[] Write() => this.ToBytes();
    private const string Battle = "Battle";
    private const string Field = "Field";
    private const string Mart = "Mart";
    private const string Heal = "Heal";

    #region Structure
    private ushort Price;

    private byte _HeldEffect;
    [Category(Battle)]
        public byte HeldEffect { get => _HeldEffect; set => _HeldEffect = value; }

    private byte _HeldArgument;
        public byte HeldArgument { get => _HeldArgument; set => _HeldArgument = value; }
    private byte _NaturalGiftEffect;
        public byte NaturalGiftEffect { get => _NaturalGiftEffect; set => _NaturalGiftEffect = value; }
    private byte _FlingEffect;
        public byte FlingEffect { get => _FlingEffect; set => _FlingEffect = value; }
    private byte _FlingPower;
        public byte FlingPower { get => _FlingPower; set => _FlingPower = value; }
    private byte _NaturalGiftPower;
        public byte NaturalGiftPower { get => _NaturalGiftPower; set => _NaturalGiftPower = value; }
    private ushort _Packed;
        public ushort Packed { get => _Packed; set => _Packed = value; }

    private byte _EffectField;
    [Category(Field), Description("Routine # to call when used; 0=unusable.")]
        public byte EffectField { get => _EffectField; set => _EffectField = value; }

    private byte _EffectBattle;
    [Category(Battle), Description("Routine # to call when used; 0=unusable.")]
        public byte EffectBattle { get => _EffectBattle; set => _EffectBattle = value; } // Battle Type

    private byte _Unk_0xC;
        public byte Unk_0xC { get => _Unk_0xC; set => _Unk_0xC = value; } // 0 or 1
    private byte _Unk_0xD;
        public byte Unk_0xD { get => _Unk_0xD; set => _Unk_0xD = value; } // Classification (0-3 Battle, 4 Balls, 5 Mail)
    private byte _Consumable;
        private byte Consumable { get => _Consumable; set => _Consumable = value; } // 4 bits for use consume, 4 bits for use not consumed
    private byte _SortIndex;
        public byte SortIndex { get => _SortIndex; set => _SortIndex = value; }
    private BattleStatusFlags _CureInflict;
        public BattleStatusFlags CureInflict { get => _CureInflict; set => _CureInflict = value; } // Bitflags
    private byte Boost0; // Revive 1, Sacred Ash 3, Rare Candy 5, EvoStone 8, upper4 for BoostAtk
    private byte Boost1; // DEF, SPA
    private byte Boost2; // SPD, SPE
    private byte Boost3; // ACC, CRIT PPUpFlags
    private ItemFlags1 _FunctionFlags0;
        public ItemFlags1 FunctionFlags0 { get => _FunctionFlags0; set => _FunctionFlags0 = value; }
    private ItemFlags2 _FunctionFlags1;
        public ItemFlags2 FunctionFlags1 { get => _FunctionFlags1; set => _FunctionFlags1 = value; }

    private sbyte _EVHP;
    [Category(Field), Description("Adds EVs to the HP stat.")]
        public sbyte EVHP { get => _EVHP; set => _EVHP = value; }

    private sbyte _EVATK;
    [Category(Field), Description("Adds EVs to the Attack stat.")]
        public sbyte EVATK { get => _EVATK; set => _EVATK = value; }

    private sbyte _EVDEF;
    [Category(Field), Description("Adds EVs to the Defense stat.")]
        public sbyte EVDEF { get => _EVDEF; set => _EVDEF = value; }

    private sbyte _EVSPE;
    [Category(Field), Description("Adds EVs to the Speed stat.")]
        public sbyte EVSPE { get => _EVSPE; set => _EVSPE = value; }

    private sbyte _EVSPA;
    [Category(Field), Description("Adds EVs to the Sp. Attack stat.")]
        public sbyte EVSPA { get => _EVSPA; set => _EVSPA = value; }

    private sbyte _EVSPD;
    [Category(Field), Description("Adds EVs to the Sp. Defense stat.")]
        public sbyte EVSPD { get => _EVSPD; set => _EVSPD = value; }

    private Heal _HealAmount;
    [Category(Heal), Description("Determines the healing percent, or if a flat value is used."), RefreshProperties(RefreshProperties.All)]
        public Heal HealAmount { get => _HealAmount; set => _HealAmount = value; }

    private byte _PPGain;
    [Category(Field), Description("PP to be added to the move's current PP if used.")]
        public byte PPGain { get => _PPGain; set => _PPGain = value; }

    private sbyte _Friendship1;
        public sbyte Friendship1 { get => _Friendship1; set => _Friendship1 = value; }
    private sbyte _Friendship2;
        public sbyte Friendship2 { get => _Friendship2; set => _Friendship2 = value; }
    private sbyte _Friendship3;
        public sbyte Friendship3 { get => _Friendship3; set => _Friendship3 = value; }
    public byte _0x23, _0x24;
    #endregion

    [Category(Mart), RefreshProperties(RefreshProperties.All)]
    public int BuyPrice { readonly get => Price * 10; set => Price = (ushort)(value / 10); }

    [Category(Mart), ReadOnly(true)]
    public int SellPrice { readonly get => Price * 5; set => Price = (ushort)(value / 5); }

    [Category(Battle)]
    public int NaturalGiftType { readonly get => Packed & 0x1F; set => Packed = (ushort)((NaturalGiftEffect & ~0x1F) | value); }

    [Category(Battle)]
    public bool Flag1 { readonly get => ((Packed >> 5) & 1) == 1; set => Packed = (ushort)((Packed & ~(1 << 5)) | ((value ? 1 : 0) << 5)); }

    [Category(Battle)]
    public bool Flag2 { readonly get => ((Packed >> 6) & 1) == 1; set => Packed = (ushort)((Packed & ~(1 << 6)) | ((value ? 1 : 0) << 6)); }

    [Category(Field)]
    public ItemPocket PocketField { readonly get => (ItemPocket)((Packed >> 7) & 0xF); set => Packed = (ushort)((Packed & 0xF87F) | (((int)value & 0xF) << 7)); }

    [Category(Battle)]
    public BattlePocket PocketBattle { readonly get => (BattlePocket)(Packed >> 11); set => Packed = (ushort)((Packed & 0x077F) | (((byte)value & 0x1F) << 11)); }

    [Category(Field)]
    public bool Revive { readonly get => ((Boost0 >> 0) & 1) == 0; set => Boost0 = (byte)((Boost0 & ~(1 << 0)) | ((value ? 1 : 0) << 0)); }

    [Category(Field)]
    public bool ReviveAll { readonly get => ((Boost0 >> 1) & 1) == 1; set => Boost0 = (byte)((Boost0 & ~(1 << 1)) | ((value ? 1 : 0) << 1)); }

    [Category(Field)]
    public bool LevelUp { readonly get => ((Boost0 >> 2) & 1) == 1; set => Boost0 = (byte)((Boost0 & ~(1 << 2)) | ((value ? 1 : 0) << 2)); }

    [Category(Field)]
    public bool EvoStone { readonly get => ((Boost0 >> 3) & 1) == 1; set => Boost0 = (byte)((Boost0 & ~(1 << 3)) | ((value ? 1 : 0) << 3)); }

    [Category(Battle)]
    public int BoostATK { readonly get => Boost0 >> 4; set => Boost0 = (byte)((Boost0 & 0xF) | (value << 4)); }

    [Category(Battle)]
    public int BoostDEF { readonly get => Boost1 & 0xF; set => Boost1 = (byte)((Boost1 & ~0xF) | (value & 0xF)); }

    [Category(Battle)]
    public int BoostSPA { readonly get => Boost1 >> 4; set => Boost1 = (byte)((Boost1 & 0xF) | (value << 4)); }

    [Category(Battle)]
    public int BoostSPD { readonly get => Boost2 & 0xF; set => Boost2 = (byte)((Boost2 & ~0xF) | (value & 0xF)); }

    [Category(Battle)]
    public int BoostSPE { readonly get => Boost2 >> 4; set => Boost2 = (byte)((Boost2 & 0xF) | (value << 4)); }

    [Category(Battle)]
    public int BoostACC { readonly get => Boost3 & 0xF; set => Boost3 = (byte)((Boost3 & ~0xF) | (value & 0xF)); }

    [Category(Battle)]
    public int BoostCRIT { readonly get => (Boost3 >> 4) & 3; set => Boost3 = (byte)((Boost3 & ~0x30) | ((value & 3) << 4)); }

    [Category(Battle)]
    public int BoostPP1 { readonly get => (Boost3 >> 6) & 1; set => Boost3 = (byte)((Boost3 & 0xBF) | ((value & 1) << 6)); }

    [Category(Battle)]
    public int BoostPPMax { readonly get => (Boost3 >> 7) & 1; set => Boost3 = (byte)((Boost3 & 0x7F) | ((value & 1) << 7)); }

    [Category(Heal), Description("Raw value of the Heal enum."), RefreshProperties(RefreshProperties.All)]
    public int HealValue
    { readonly get => (int)HealAmount;
        set => HealAmount = (Heal)value;
    }

    [Category(Heal), Description("Item is consumed when used."), RefreshProperties(RefreshProperties.All)]
    public bool UseConsume { readonly get => (Consumable & 0xF) != 0; set => Consumable = (byte)((Consumable & 0xF0) | (value ? 1 : 0)); }

    [Category(Heal), Description("Item is not consumed when used."), RefreshProperties(RefreshProperties.All)]
    public bool UseKeep { readonly get => (Consumable & 0xF0) != 0; set => Consumable = (byte)((Consumable & 0x0F) | (value ? 0x10 : 0)); }
}

[Flags]
public enum ItemFlags1 : byte
{
    None,
    RestorePP = 1 << 0,
    RestorePPAll = 1 << 1,
    RestoreHP = 1 << 2,
    AddEVHP = 1 << 3,
    AddEVAtk = 1 << 4,
    AddEVDef = 1 << 5,
    AddEVSpe = 1 << 6,
    AddEVSpA = 1 << 7,
}

[Flags]
public enum ItemFlags2 : byte
{
    None,
    AddEVSpD = 1 << 0,
    AddEVAbove100 = 1 << 1,
    AddFriendship1 = 1 << 2,
    AddFriendship2 = 1 << 3,
    AddFriendship3 = 1 << 4,
    Unused1 = 1 << 5,
    Unused2 = 1 << 6,
    Unused3 = 1 << 7,
}

[Flags]
public enum BattlePocket : byte
{
    None,
    Ball = 1 << 0,
    Boosts = 1 << 1,
    Restore = 1 << 2,
    Misc = 1 << 3,
}

public enum ItemPocket : byte
{
    None = 0,
    Items = 1,
    TMs = 2,
    Medicine = 3,
    Berries = 4,
    KeyItems = 5,
    ZCrystals = 6,
    BattleItems = 7,
    FreeSpace = 8
}