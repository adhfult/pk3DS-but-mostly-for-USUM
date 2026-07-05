using System.IO;

namespace pk3DS.Core.CTR
{
    public interface IGARCFileProvider
    {
        int FileCount { get; }
        int GetFileLength(int index);
        void WriteFile(int index, BinaryWriter gw);
    }
}
