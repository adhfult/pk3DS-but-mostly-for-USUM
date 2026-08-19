using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core;
using pk3DS.Core.CTR;
using pk3DS.Core.Structures;
using pk3DS.Core.Structures.PersonalInfo;

namespace pk3DS.WinForms;

public partial class FormInsertion : Form
{
    private readonly byte[][] personalFiles;
    private readonly byte[][] evolutionFiles;
    private readonly byte[][] levelupFiles;
    private readonly byte[][] eggmoveFiles;
    private readonly string[] speciesNames;
    private readonly string[] entryNames;
    private readonly int[] baseForms;
    private readonly int[] formVal;

    public FormInsertion(byte[][] personal, byte[][] evolution, byte[][] levelup, byte[][] eggmoves, string[] species, string[] entries, int[] bases, int[] forms)
    {
        if (personal.Length > 0 && personal.Last().Length != personal[0].Length)
        {
            personal = personal.Take(personal.Length - 1).ToArray();
        }
        personalFiles = personal;
        evolutionFiles = evolution;
        levelupFiles = levelup;
        eggmoveFiles = eggmoves;
        speciesNames = species;
        entryNames = entries;
        baseForms = bases;
        formVal = forms;

        if (baseForms == null || baseForms.Length < personalFiles.Length)
        {
            int oldLen = baseForms?.Length ?? 0;
            Array.Resize(ref baseForms, personalFiles.Length);
            for (int i = oldLen; i < personalFiles.Length; i++) baseForms[i] = i;
        }
        if (formVal == null || formVal.Length < personalFiles.Length)
        {
            int oldLen = formVal?.Length ?? 0;
            Array.Resize(ref formVal, personalFiles.Length);
            for (int i = oldLen; i < personalFiles.Length; i++) formVal[i] = 0;
        }

        InitializeComponent();

        CB_TargetSpecies.Items.AddRange(speciesNames.Take(Math.Min(speciesNames.Length, Main.Config.MaxSpeciesID + 1)).ToArray());
        CB_TargetSpeciesEnd.Items.AddRange(speciesNames.Take(Math.Min(speciesNames.Length, Main.Config.MaxSpeciesID + 1)).ToArray());
        CB_CopyFrom.Items.AddRange(entryNames);
        
        CB_TargetSpecies.SelectedIndex = 1;
        CB_TargetSpeciesEnd.SelectedIndex = 1;
        CB_CopyFrom.SelectedIndex = 1;
        
        RTB_BatchList.Text = "Bulbasaur\nIvysaur\nVenusaur";
    }

    private void B_Insert_Click(object sender, EventArgs e)
    {
        try
        {
            List<int> speciesToInsert = new List<int>();
            if (CHK_BatchList.Checked)
            {
                string[] names = RTB_BatchList.Lines;
                foreach (string name in names)
                {
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    int idx = Array.FindIndex(speciesNames, s => s.Equals(name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (idx > 0) speciesToInsert.Add(idx);
                }
            }
            else if (CHK_Batch.Checked)
            {
                int start = CB_TargetSpecies.SelectedIndex;
                int end = CB_TargetSpeciesEnd.SelectedIndex;
                if (start > 0 && end >= start)
                {
                    for (int i = start; i <= end; i++) speciesToInsert.Add(i);
                }
            }
            else
            {
                int idx = CB_TargetSpecies.SelectedIndex;
                if (idx > 0) speciesToInsert.Add(idx);
            }

            if (speciesToInsert.Count == 0) { WinFormsUtil.Error("No valid species selected for insertion."); return; }
            int count = (int)NUD_FormCount.Value;
            bool isBatch = CHK_BatchList.Checked || CHK_Batch.Checked;
            int copyIndex = isBatch ? -1 : CB_CopyFrom.SelectedIndex;

            if (!isBatch && copyIndex < 0) { WinFormsUtil.Error("Please select a template species to copy from."); return; }

            // Validate bounds - copyIndex is from entryNames (includes forms), not speciesNames
            if (!isBatch && copyIndex >= personalFiles.Length)
            { WinFormsUtil.Error($"Template index {copyIndex} exceeds personal data bounds ({personalFiles.Length})."); return; }

            string speciesSummary = speciesToInsert.Count > 1
                ? $"{speciesToInsert.Count} species"
                : (speciesToInsert[0] < speciesNames.Length ? speciesNames[speciesToInsert[0]] : $"Entry #{speciesToInsert[0]}");
            
            string templateName = isBatch ? "Base Form of each Species" : (copyIndex < entryNames.Length ? entryNames[copyIndex] : $"Entry #{copyIndex}");

            if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Insert {count} forms for {speciesSummary}?", "Template: " + templateName) != DialogResult.Yes)
                return;

            // 0. (Removed BackupCriticalFiles to prevent .bak interference with rebuilding)

            foreach (int sID in speciesToInsert)
            {
                if (sID < personalFiles.Length && personalFiles[sID] != null && personalFiles[sID].Length > 0x20)
                {
                    int curForms = personalFiles[sID][0x20];
                    if (curForms + count > 255)
                    {
                        WinFormsUtil.Error($"Form count overflow for species #{sID}. Maximum forms per species is 255 (current: {curForms}, adding: {count}).");
                        return;
                    }
                }
            }

            int projected = personalFiles.Length + (speciesToInsert.Count * count);
            int ceiling = GetPersonalEntryCeiling();
            if (projected > ceiling)
            {
                int room = Math.Max(0, ceiling - personalFiles.Length);
                WinFormsUtil.Error(
                    $"This would grow the personal table to {projected} entries, past the {ceiling} this ROM supports.",
                    $"The table currently holds {personalFiles.Length} entries, so there is room for {room} more.\n\n" +
                    $"You asked for {speciesToInsert.Count} species x {count} forms = {speciesToInsert.Count * count} entries.\n\n" +
                    "Raising the limit means repointing the icon preload count in code.bin, which this editor does not do.");
                return;
            }

            foreach (int sID in speciesToInsert)
            {
                int finalTemplate = isBatch ? sID : copyIndex;
                InsertForms(sID, count, finalTemplate);
                // Synchronize lists for next iteration
                personalFilesList = ResultPersonal.ToList();
                evolutionFilesList = ResultEvolution.ToList();
                levelupFilesList = ResultLevelUp.ToList();
                eggmoveFilesList = ResultEggMoves.ToList();
            }

            WinFormsUtil.Alert("Insertion complete!", $"Added forms for {speciesToInsert.Count} species.");
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Insertion failed.", ex.Message + "\n" + ex.StackTrace);
        }
    }

    /// <summary>Icon-member count a retail USUM build preloads.</summary>
    private const int RetailPersonalEntryCeiling = 1153;

    /// <summary>Value the Expansion Pack writes in its place.</summary>
    private const int ExpansionPersonalEntryCeiling = 1507;

    /// <summary>
    /// How many personal entries this ROM can actually carry, read from the icon preload count in
    /// code.bin rather than assumed, so a build that raised it further is respected and one that
    /// never raised it at all is not overrun.
    /// </summary>
    private static int GetPersonalEntryCeiling()
    {
        // Offsets the code map records for the icon preload word, US and UM. Each is verified to
        // hold a plausible count before being believed, so a wrong offset falls through.
        foreach (int ofs in new[] { 0x1D4C2C, 0x1D4C24, 0x1DF164 })
        {
            try
            {
                if (Main.ExeFSPath == null) break;
                string[] files = System.IO.Directory.GetFiles(Main.ExeFSPath);
                string codeFile = files.FirstOrDefault(f => System.IO.Path.GetFileNameWithoutExtension(f)
                    .Contains("code", StringComparison.OrdinalIgnoreCase));
                if (codeFile == null) break;

                byte[] code = System.IO.File.ReadAllBytes(codeFile);
                if (ofs + 4 > code.Length) continue;
                uint v = BitConverter.ToUInt32(code, ofs);
                if (v is >= RetailPersonalEntryCeiling and <= 8192) return (int)v;
            }
            catch { break; }
        }
        return ExpansionPersonalEntryCeiling;
    }

    private void InsertFormsBatch(int start, int end, int count, int templateID)
    {
        // For batch, we do it one by one but update the table in between
        // Note: each insertion shifts the table, but since we are inserting for BASE species, 
        // their IDs 1-Main.Config.MaxSpeciesID DO NOT CHANGE. 
        // Only the forms at the end of the table shift.

        for (int i = start; i <= end; i++)
        {
            InsertForms(i, count, templateID);
            // Refresh our local files with the results so the next iteration uses the updated pointers
            personalFilesList = ResultPersonal.ToList();
            evolutionFilesList = ResultEvolution.ToList();
            levelupFilesList = ResultLevelUp.ToList();
            eggmoveFilesList = ResultEggMoves.ToList();
        }
    }

    private List<byte[]> personalFilesList;
    private List<byte[]> evolutionFilesList;
    private List<byte[]> levelupFilesList;
    private List<byte[]> eggmoveFilesList;

    private void InsertForms(int speciesID, int count, int templateID)
    {
        if (personalFilesList == null)
        {
            personalFilesList = personalFiles.ToList();
            evolutionFilesList = evolutionFiles.ToList();
            levelupFilesList = levelupFiles.ToList();
            eggmoveFilesList = eggmoveFiles.ToList();
        }

        // 1. Get current forme count and pointer
        byte[] baseData = personalFilesList[speciesID];
        int currentCount = baseData[0x20];
        int currentPointer = BitConverter.ToUInt16(baseData, 0x1C);

        List<byte[]> newPersonal = [.. personalFilesList];
        List<byte[]> newEvolution = [.. evolutionFilesList];
        List<byte[]> newLevelUp = [.. levelupFilesList];
        List<byte[]> newEggMoves = [.. eggmoveFilesList];

        // 1b. Synchronize list lengths (Padding)
        while (newEvolution.Count < newPersonal.Count) newEvolution.Add(new byte[EvolutionSet7.SIZE]);
        while (newLevelUp.Count < newPersonal.Count) newLevelUp.Add(new byte[0]);
        bool eggMovesHasForms = newEggMoves.Count >= newPersonal.Count;
        if (eggMovesHasForms)
        {
            while (newEggMoves.Count < newPersonal.Count) newEggMoves.Add(new byte[0]);
        }

        // 1c. Calculate Insertion Index with safety checks
        int insertionIndex;
        if (currentCount > 1 && currentPointer > 0 && currentPointer < personalFilesList.Count)
        {
            // Already has forms, append to the end of the existing forms block
            insertionIndex = currentPointer + currentCount - 1;
        }
        else
        {
            // We are adding the FIRST alternative form for this species.
            // Find its chronological place in the alternative forms section.
            insertionIndex = -1;
            
            // Look forward to find the FIRST species after this one that already has alternative forms
            for (int i = speciesID + 1; i <= Main.Config.MaxSpeciesID; i++)
            {
                int ptr = BitConverter.ToUInt16(personalFilesList[i], 0x1C);
                if (ptr > 0 && ptr < personalFilesList.Count)
                {
                    insertionIndex = ptr;
                    break;
                }
            }
            
            // If no subsequent species have alt forms, append to the very end
            if (insertionIndex == -1)
            {
                insertionIndex = newPersonal.Count;
            }
            
            currentPointer = insertionIndex;
        }

        // Final safety clamp
        if (insertionIndex > newPersonal.Count) insertionIndex = newPersonal.Count;
        if (insertionIndex < 0) insertionIndex = 0;

        // Safety check: If currentCount was manually inflated in the editor without actual forms existing,
        // clamp it so we don't iterate out of bounds later.
        if (currentPointer + currentCount - 1 > newPersonal.Count)
        {
            currentCount = newPersonal.Count - currentPointer + 1;
            if (currentCount < 1) currentCount = 1;
        }

        // 2. Prepare template data
        byte[] personalTemplate = (byte[])personalFilesList[templateID].Clone();
        byte[] evolutionTemplate = (byte[])evolutionFilesList[templateID].Clone();
        byte[] levelupTemplate = (byte[])levelupFilesList[templateID].Clone();

        // 3. Insert new entries and shift EVERYTHING
        for (int i = 0; i < count; i++)
        {
            newPersonal.Insert(insertionIndex + i, (byte[])personalTemplate.Clone());
            byte[] evoClone = templateID < newEvolution.Count ? (byte[])newEvolution[templateID].Clone() : new byte[EvolutionSet7.SIZE];
            newEvolution.Insert(insertionIndex + i, evoClone);
            
            byte[] lvlClone = templateID < newLevelUp.Count ? (byte[])newLevelUp[templateID].Clone() : new byte[0];
            newLevelUp.Insert(insertionIndex + i, lvlClone);
            
            if (eggMovesHasForms && newEggMoves.Count > 0)
            {
                byte[] eggClone = templateID < newEggMoves.Count ? (byte[])newEggMoves[templateID].Clone() : new byte[0];
                newEggMoves.Insert(insertionIndex + i, eggClone);
            }
        }

        // 4. Update the base and ALL forms (old + new) with the new count and pointer
        int newTotalCount = currentCount + count;
        var familyIndices = new List<int> { speciesID };
        for (int i = 0; i < currentCount - 1; i++) familyIndices.Add(currentPointer + i);
        for (int i = 0; i < count; i++) familyIndices.Add(insertionIndex + i);

        foreach (int idx in familyIndices)
        {
            // Note: idx is relative to the OLD table size if idx < insertionIndex, 
            // but we need to find them in the NEW table.
            int realIdx = idx >= insertionIndex ? idx + count : idx; 
            // Wait, this is confusing. Let's just use the family list we just built.
        }

        // Simpler: Just refresh the whole family in the NEW list.
        for (int i = 0; i < newTotalCount - 1; i++)
        {
            byte[] data = newPersonal[currentPointer + i];
            data[0x20] = (byte)newTotalCount;
            byte[] ptrBytes = BitConverter.GetBytes((ushort)currentPointer);
            data[0x1C] = ptrBytes[0];
            data[0x1D] = ptrBytes[1];
        }
        // Base species too
        newPersonal[speciesID][0x20] = (byte)newTotalCount;
        byte[] bPtrBytes = BitConverter.GetBytes((ushort)currentPointer);
        newPersonal[speciesID][0x1C] = bPtrBytes[0];
        newPersonal[speciesID][0x1D] = bPtrBytes[1];

        // 5. Global Pointer Realignment
        RealignAllPointers(newPersonal, insertionIndex, count, speciesID);

        // 6. Update Model GARC (header + files)
        UpdateModelGARC(speciesID, count, templateID);

        ResultPersonal = [.. newPersonal];
        ResultEvolution = [.. newEvolution];
        ResultLevelUp = [.. newLevelUp];
        ResultEggMoves = [.. newEggMoves];
    }

    private void RealignAllPointers(List<byte[]> personal, int insertionAt, int count, int excludeSpecies)
    {
        for (int i = 1; i < personal.Count; i++)
        {
            if (i == excludeSpecies) continue;
            if (i >= insertionAt && i < insertionAt + count) continue; // Skip newly inserted forms

            int ptr = BitConverter.ToUInt16(personal[i], 0x1C);
            if (ptr == 0) continue;

            if (ptr >= insertionAt)
            {
                ptr += count;
                byte[] ptrBytes = BitConverter.GetBytes((ushort)ptr);
                personal[i][0x1C] = ptrBytes[0];
                personal[i][0x1D] = ptrBytes[1];
            }
        }
    }

    private int GetModelBinsPerForm(GARC.LazyGARC garc, int total_forms)
    {
        return (garc.FileCount - 1) / total_forms;
    }

    private string GetModelGARCPath()
    {
        if (Main.Config.USUM) return Path.Combine(Main.RomFSPath, "a", "0", "9", "4");
        if (Main.Config.Sun || Main.Config.Moon) return Path.Combine(Main.RomFSPath, "a", "0", "9", "3");
        if (Main.Config.ORAS) return Path.Combine(Main.RomFSPath, "a", "0", "0", "8");
        if (Main.Config.XY) return Path.Combine(Main.RomFSPath, "a", "0", "0", "7");
        return null;
    }

    private class FormInsertionFileProvider : pk3DS.Core.CTR.IGARCFileProvider, IDisposable
    {
        private readonly GARC.LazyGARC _baseGarc;
        private readonly byte[] _newHeader;
        private readonly List<byte[]> _newBins;
        private readonly int _insertIndex;
        private readonly int _addedCount;
        private readonly FileStream _fs;
        private readonly object _fsLock = new object();
        private readonly int _paddingToAdd;

        public FormInsertionFileProvider(GARC.LazyGARC baseGarc, byte[] newHeader, List<byte[]> newBins, int insertIndex, int paddingToAdd)
        {
            _baseGarc = baseGarc;
            _newHeader = newHeader;
            _newBins = newBins;
            _insertIndex = insertIndex;
            _addedCount = newBins.Count;
            _paddingToAdd = paddingToAdd;
            if (_baseGarc.FilePath != null)
            {
                _fs = new FileStream(_baseGarc.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
        }

        public int FileCount => _baseGarc.FileCount + _addedCount + _paddingToAdd;

        public int GetFileLength(int index)
        {
            if (index == 0) return _newHeader.Length;
            if (index >= _insertIndex && index < _insertIndex + _addedCount)
                return _newBins[index - _insertIndex].Length;

            int originalIndex = index > _insertIndex ? index - _addedCount : index;
            if (originalIndex >= _baseGarc.FileCount) return 0; // Empty padding bins

            var entry = _baseGarc.garc.fatb.Entries[originalIndex];
            var subEntry = entry.SubEntries.FirstOrDefault(s => s.Exists);
            return subEntry.Exists ? (int)subEntry.Length : 0;
        }

        public void WriteFile(int index, BinaryWriter gw)
        {
            if (index == 0)
            {
                gw.Write(_newHeader);
                return;
            }
            if (index >= _insertIndex && index < _insertIndex + _addedCount)
            {
                gw.Write(_newBins[index - _insertIndex]);
                return;
            }

            int originalIndex = index > _insertIndex ? index - _addedCount : index;
            if (originalIndex >= _baseGarc.FileCount) return; // Padding bins are empty

            var entry = _baseGarc.garc.fatb.Entries[originalIndex];
            var subEntry = entry.SubEntries.FirstOrDefault(s => s.Exists);
            if (!subEntry.Exists) return;

            if (_fs != null)
            {
                int length = (int)subEntry.Length;
                byte[] buffer = new byte[length];
                lock (_fsLock)
                {
                    _fs.Seek(subEntry.Start + _baseGarc.garc.DataOffset, SeekOrigin.Begin);
                    _fs.Read(buffer, 0, length);
                }
                gw.Write(buffer);
                return;
            }

            // Fallback if no FileStream (shouldn't happen)
            gw.Write(_baseGarc[originalIndex]);
        }

        public void Dispose()
        {
            _fs?.Dispose();
        }
    }

    private void UpdateModelGARC(int species, int addedCount, int templateID)
    {
        string path = GetModelGARCPath();
        if (path == null || !File.Exists(path)) return;

        GARC.LazyGARC garc = new GARC.LazyGARC(path);
        List<byte> headerList = new List<byte>(garc[0]);

        int reqHeaderBytes = (Main.Config.MaxSpeciesID + 1) * 4;
        if (headerList.Count < reqHeaderBytes)
        {
            int diff = reqHeaderBytes - headerList.Count;
            headerList.AddRange(new byte[diff]);
        }

        int total_forms = 0;
        for (int i = 0; i <= Main.Config.MaxSpeciesID; i++)
            total_forms += headerList[i * 4 + 2];
            
        if (total_forms <= 0) total_forms = 1;
        int model_file_count = GetModelBinsPerForm(garc, total_forms);
        if (model_file_count <= 0) return;
        
        // Byte 2 is total forms for species, byte 0-1 is sum of all FORMS prior
        int forms_for_species = headerList[species * 4 + 2];
        int sum_forms_prior = BitConverter.ToUInt16(headerList.ToArray(), species * 4);
        int total_previous_forms = sum_forms_prior + forms_for_species;

        if (headerList[species * 4 + 3] < 0x05)
            headerList[species * 4 + 3] += 0x04;

        headerList[species * 4 + 2] += (byte)addedCount;

        int maxSpecies = Main.Config.MaxSpeciesID;
        for (int i = 0; i <= maxSpecies; i++)
        {
            if (i == species) continue;
            int prior = BitConverter.ToUInt16(headerList.ToArray(), i * 4);
            if (prior >= total_previous_forms)
            {
                prior += addedCount;
                byte[] bytes = BitConverter.GetBytes((ushort)prior);
                headerList[i * 4] = bytes[0];
                headerList[i * 4 + 1] = bytes[1];
            }
        }

        int start_of_byte_flag_table = 4 * (Main.Config.MaxSpeciesID + 1);

        int model_source_index;
        if (templateID <= Main.Config.MaxSpeciesID)
        {
            model_source_index = BitConverter.ToUInt16(headerList.ToArray(), templateID * 4);
        }
        else
        {
            int baseID = baseForms[templateID];
            int fVal = formVal[templateID];
            model_source_index = BitConverter.ToUInt16(headerList.ToArray(), baseID * 4) + fVal;
        }
        
        int model_source_flag_offset = 2 * model_source_index + start_of_byte_flag_table;
        byte flag_0 = headerList[model_source_flag_offset];
        byte flag_1 = headerList[model_source_flag_offset + 1];

        int target_bitflag_offset = 2 * total_previous_forms + start_of_byte_flag_table;
        for (int i = 0; i < addedCount; i++)
        {
            headerList.Insert(target_bitflag_offset, flag_1); // Insert reversed to push correctly
            headerList.Insert(target_bitflag_offset, flag_0);
        }

        byte[] newHeader = headerList.ToArray();

        int model_start_file = 0;
        int model_dest_file = 0;

        if (Main.Config.XY || Main.Config.ORAS)
        {
            int offset = Main.Config.XY ? 3 : 2;
            model_start_file = model_file_count * model_source_index + offset;
            model_dest_file = model_file_count * total_previous_forms + offset;
        }
        else
        {
            model_start_file = model_file_count * model_source_index + 1;
            model_dest_file = model_file_count * total_previous_forms + 1;
        }

        List<byte[]> tempBins = new List<byte[]>();
        if (model_start_file + model_file_count <= garc.FileCount)
        {
            using (var fs = File.OpenRead(garc.FilePath))
            {
                for (int j = 0; j < model_file_count; j++)
                {
                    var entry = garc.garc.fatb.Entries[model_start_file + j];
                    var subEntry = entry.SubEntries.FirstOrDefault(s => s.Exists);
                    if (!subEntry.Exists) 
                    {
                        tempBins.Add(new byte[0]);
                        continue;
                    }
                    int length = (int)subEntry.Length;
                    byte[] buffer = new byte[length];
                    fs.Seek(subEntry.Start + garc.garc.DataOffset, SeekOrigin.Begin);
                    fs.Read(buffer, 0, length);
                    tempBins.Add(buffer);
                }
            }
        }
        else
        {
            // Fallback: Use empty bins if the template is out of bounds
            for (int j = 0; j < model_file_count; j++)
                tempBins.Add(new byte[0]);
        }

        int paddingToAdd = 0;
        if (model_dest_file > garc.FileCount)
        {
            paddingToAdd = model_dest_file - garc.FileCount;
        }

        List<byte[]> binsToInsert = new List<byte[]>();
        for (int i = 0; i < addedCount; i++)
        {
            for (int j = 0; j < model_file_count; j++)
                binsToInsert.Add((byte[])tempBins[j].Clone());
        }

        string tempPath = Path.GetTempFileName();
        using (var provider = new FormInsertionFileProvider(garc, newHeader, binsToInsert, model_dest_file, paddingToAdd))
        {
            GARC.PackGARC(provider, tempPath, garc.garc.Version, (int)garc.garc.ContentPadToNearest);
        }

        File.Copy(tempPath, path, true);
        File.Delete(tempPath);
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }



    // (Removed ExpandGameText to prevent text corruption)

    public byte[][] ResultPersonal;
    public byte[][] ResultEvolution;
    public byte[][] ResultLevelUp;
    public byte[][] ResultEggMoves;

}
