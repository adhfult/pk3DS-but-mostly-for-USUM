using System;
using System.Collections.Generic;

namespace pk3DS.Core.Structures
{
    public class OWTrainerFile
    {
        public byte[] RawData;
        public List<List<OWTrainerEntry>> Areas;
        public int AreaCount;

        public OWTrainerFile(byte[] data)
        {
            RawData = (byte[])data.Clone();
            Areas = new List<List<OWTrainerEntry>>();

            ushort magic = BitConverter.ToUInt16(RawData, 0x00);
            if (magic != 0x5445) // 'E', 'T' (Little Endian -> 0x5445)
            {
                return;
            }

            AreaCount = RawData[0x02];

            int offsetIndex = 0x04;
            uint[] sectionOffsets = new uint[AreaCount];
            for (int i = 0; i < AreaCount; i++)
            {
                sectionOffsets[i] = BitConverter.ToUInt32(RawData, offsetIndex);
                offsetIndex += 4;
            }

            uint totalSize = BitConverter.ToUInt32(RawData, offsetIndex);
            offsetIndex += 4;

            for (int a = 0; a < AreaCount; a++)
            {
                var areaList = new List<OWTrainerEntry>();
                uint sectionEnd = (a + 1 < AreaCount) ? sectionOffsets[a + 1] : totalSize;
                if (sectionEnd > RawData.Length) sectionEnd = (uint)RawData.Length;

                if (sectionOffsets[a] < (uint)RawData.Length)
                {
                    // Scan for magic bytes: 04 (stationary), 07 (trainer), 08 (mobile)
                    for (int currentOffset = (int)sectionOffsets[a]; currentOffset >= 0; currentOffset += 4)
                    {
                        // Hard stop: must have room for the full 84-byte struct in the actual data
                        if (currentOffset + OWTrainerEntry.SIZE > RawData.Length) break;
                        // Also stop if we've passed the section boundary
                        if ((uint)currentOffset + OWTrainerEntry.SIZE > sectionEnd) break;

                        uint magicType = BitConverter.ToUInt32(RawData, currentOffset);
                        if (magicType == 0x04 || magicType == 0x07 || magicType == 0x08)
                        {
                            var entry = new OWTrainerEntry(RawData, currentOffset);
                            if (entry.IsValid())
                            {
                                areaList.Add(entry);
                                currentOffset += OWTrainerEntry.SIZE - 4; // Skip the rest of the 84-byte struct
                            }
                        }
                    }
                }

                Areas.Add(areaList);
            }
        }

        public byte[] Write()
        {
            byte[] newData = (byte[])RawData.Clone();

            foreach (var area in Areas)
            {
                foreach (var entry in area)
                {
                    Array.Copy(entry.RawData, 0, newData, entry.Offset, OWTrainerEntry.SIZE);
                }
            }

            return newData;
        }
    }
}
