using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pk3DS.Core.CTR;
using pk3DS.Core.Modding;

namespace pk3DS.Core.Randomizers;

public class TutorRandomizer
{
    private readonly string RomFSPath;
    private readonly int Mode; // 1: Random

    private static readonly uint[] TutorPatchAddrs = { 0x4C8, 0x4D4, 0x4E0, 0x4EC };
    private static readonly int[] DefaultLengths = { 16, 17, 16, 18 };

    public TutorRandomizer(string romfsPath, int mode)
    {
        RomFSPath = romfsPath;
        Mode = mode;
    }

    /// <summary>
    /// Randomizes (if Mode != 0) or simply reads (if Mode == 0) the move tutor lists.
    /// Returns the resulting per-category move ID lists (index 0 = type tutors, 1-3 = special
    /// tutors) so callers can feed them into the competitive legal-movepool aggregator, or null
    /// if the tutor data couldn't be located (e.g. RomFS not extracted).
    /// </summary>
    public ushort[][] Execute(int maxMoveID, MoveRandomizer moveRand, GameConfig config = null, bool isCompetitive = false)
    {
        string croPath = Path.Combine(RomFSPath, "Shop.cro");
        if (!File.Exists(croPath)) return null;

        string[] moveNames = config != null ? config.GetText(TextName.MoveNames) : [];
        byte[] data = File.ReadAllBytes(croPath);

        // Build valid move list excluding Z-moves (and restricted to CompetitiveMoves if requested)
        var validMoveList = new List<int>();
        if (Mode != 0 && moveNames != null && moveNames.Length > 0)
        {
            int limit = maxMoveID > 0 ? Math.Min(maxMoveID, moveNames.Length) : moveNames.Length;
            for (int m = 1; m < limit; m++)
            {
                if (Legal.Z_Moves.Contains(m)) continue;
                string name = moveNames[m];
                if (string.IsNullOrWhiteSpace(name) || name == "—" || name == "———") continue;
                if (isCompetitive && !Competitive.CompetitiveDatabase.CompetitiveMoves.Contains(name) && !Competitive.CompetitiveDatabase.SituationalMoves.Contains(name)) continue;
                validMoveList.Add(m);
            }
        }

        var result = new ushort[TutorPatchAddrs.Length][];
        bool anyOffsetFound = false;

        for (int i = 0; i < TutorPatchAddrs.Length; i++)
        {
            int rptOfs = ResearchEngine.GetRelocationPatchTarget(data, TutorPatchAddrs[i]);
            int baseOfs = rptOfs != -1 ? rptOfs : (0x54DE + (i * 16 * 4));
            result[i] = new ushort[DefaultLengths[i]];
            if (baseOfs <= 0 || baseOfs + (DefaultLengths[i] * 4) > data.Length) continue;
            anyOffsetFound = true;

            for (int m = 0; m < DefaultLengths[i]; m++)
            {
                int m_ofs = baseOfs + (m * 4);

                if (Mode == 0)
                {
                    // Read-only: report the move currently sitting in this tutor slot.
                    result[i][m] = BitConverter.ToUInt16(data, m_ofs);
                    continue;
                }

                int move;
                if (validMoveList.Count > 0)
                {
                    move = validMoveList[Util.Rand.Next(validMoveList.Count)];
                }
                else
                {
                    move = (int)(Util.Random32() % (uint)(maxMoveID > 0 ? maxMoveID : 720)) + 1;
                    while (Legal.Z_Moves.Contains(move))
                        move = (int)(Util.Random32() % (uint)(maxMoveID > 0 ? maxMoveID : 720)) + 1;
                }

                result[i][m] = (ushort)move;
                BitConverter.GetBytes((ushort)move).CopyTo(data, m_ofs);
            }
        }

        if (!anyOffsetFound) return null;

        if (Mode != 0)
        {
            CROUtil.UpdateHashes(data);
            File.WriteAllBytes(croPath, data);
        }

        return result;
    }
}
