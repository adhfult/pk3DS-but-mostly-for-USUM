using System;

namespace pk3DS.Core.Structures
{
    public class OWTrainerEntry
    {
        public const int SIZE = 84;
        public byte[] RawData;
        public int Offset;

        public OWTrainerEntry(byte[] data, int offset)
        {
            RawData = new byte[SIZE];
            Array.Copy(data, offset, RawData, 0, SIZE);
            Offset = offset;
        }

        public uint ObjectType
        {
            get => BitConverter.ToUInt32(RawData, 0x00);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x00);
        }

        public float X
        {
            get => BitConverter.ToSingle(RawData, 0x04);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x04);
        }

        public float Y
        {
            get => BitConverter.ToSingle(RawData, 0x08);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x08);
        }

        public float Z
        {
            get => BitConverter.ToSingle(RawData, 0x0C);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x0C);
        }

        public float RotW
        {
            get => BitConverter.ToSingle(RawData, 0x10);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x10);
        }

        public float RotX
        {
            get => BitConverter.ToSingle(RawData, 0x14);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x14);
        }

        public float RotY
        {
            get => BitConverter.ToSingle(RawData, 0x18);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x18);
        }

        public float RotZ
        {
            get => BitConverter.ToSingle(RawData, 0x1C);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x1C);
        }

        public uint RomVersion
        {
            get => BitConverter.ToUInt32(RawData, 0x20);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x20);
        }

        public uint Flagwork
        {
            get => BitConverter.ToUInt32(RawData, 0x24);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x24);
        }

        public uint FlagworkNum
        {
            get => BitConverter.ToUInt32(RawData, 0x28);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x28);
        }

        public uint EventID
        {
            get => BitConverter.ToUInt32(RawData, 0x2C);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x2C);
        }

        public uint ModelID
        {
            get => BitConverter.ToUInt32(RawData, 0x30);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x30);
        }

        public uint BattleScriptID
        {
            get => BitConverter.ToUInt32(RawData, 0x34);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x34);
        }

        public uint Alias
        {
            get => BitConverter.ToUInt32(RawData, 0x38);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x38);
        }

        public uint MoveDataOffset
        {
            get => BitConverter.ToUInt32(RawData, 0x3C);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x3C);
        }

        public uint TalkCollisionOffset
        {
            get => BitConverter.ToUInt32(RawData, 0x40);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x40);
        }

        public uint CollisionOffset
        {
            get => BitConverter.ToUInt32(RawData, 0x44);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x44);
        }

        public uint SignalOffset
        {
            get => BitConverter.ToUInt32(RawData, 0x48);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x48);
        }

        public uint PathCoreOffset
        {
            get => BitConverter.ToUInt32(RawData, 0x4C);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x4C);
        }

        public uint PathPointOffset
        {
            get => BitConverter.ToUInt32(RawData, 0x50);
            set => BitConverter.GetBytes(value).CopyTo(RawData, 0x50);
        }

        public bool IsValid()
        {
            // Sanity check to avoid false positives when scanning for magic bytes
            if (ModelID > 10000) return false;
            if (EventID > 10000) return false;
            
            // Offsets shouldn't be astronomically large (file size is usually a few KB)
            // Note: Unused offsets might be 0xFFFFFFFF (4294967295)
            if (MoveDataOffset > 1000000 && MoveDataOffset != 0xFFFFFFFF) return false;
            if (TalkCollisionOffset > 1000000 && TalkCollisionOffset != 0xFFFFFFFF) return false;
            if (CollisionOffset > 1000000 && CollisionOffset != 0xFFFFFFFF) return false;
            if (SignalOffset > 1000000 && SignalOffset != 0xFFFFFFFF) return false;
            if (PathCoreOffset > 1000000 && PathCoreOffset != 0xFFFFFFFF) return false;
            if (PathPointOffset > 1000000 && PathPointOffset != 0xFFFFFFFF) return false;

            // X and Z coordinates are usually within reasonable bounds for a map
            // Subnormal floats (e.g. 1E-44) indicate garbage data from a misaligned read
            if (float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z)) return false;
            if (float.IsSubnormal(X) || float.IsSubnormal(Y) || float.IsSubnormal(Z)) return false;
            if (Math.Abs(X) > 1000000 || Math.Abs(Y) > 1000000 || Math.Abs(Z) > 1000000) return false;

            return true;
        }
    }
}
