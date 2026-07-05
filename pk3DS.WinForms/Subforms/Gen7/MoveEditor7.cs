using pk3DS.Core;
using pk3DS.Core.CTR;
using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using pk3DS.Core.Structures;
using pk3DS.Core.Randomizers;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace pk3DS.WinForms;

public partial class MoveEditor7 : Form
{
    private bool loading = true;
    private byte[][] files;
    public byte[][] Files => files;
    private string[] types = Main.Config.GetText(TextName.Types);
    private string[] moveflavor = Main.Config.GetText(TextName.MoveFlavor);
    private string[] movelist = Main.Config.GetText(TextName.MoveNames);
    private byte[][] _originalInfiles;
    private static byte[][] _sessionMoveData;
    private Dictionary<int, int> AnimationMap = new Dictionary<int, int>();
    private Dictionary<string, int> AnimHashes = new Dictionary<string, int>();
    private string AnimationMapPath => Path.Combine(Path.GetDirectoryName(Main.RomFSPath) ?? "", "move_anims.json");
    private byte[][] animFiles;

    public MoveEditor7(byte[][] infiles)
    {
        _originalInfiles = infiles;
        _originalMoves = (byte[][])infiles.Clone();

        // 1. Load from session cache if it exists, otherwise copy from main memory
        if (_sessionMoveData != null && _sessionMoveData.Length >= infiles.Length)
        {
            files = new byte[_sessionMoveData.Length][];
            for (int i = 0; i < _sessionMoveData.Length; i++)
                files[i] = (byte[])_sessionMoveData[i].Clone();
        }
        else
        {
            files = new byte[infiles.Length][];
            for (int i = 0; i < infiles.Length; i++)
                files[i] = (byte[])infiles[i].Clone();
        }

        // 2. Auto-Sync Routine: Expands the Move GARC to match Game Text
        if (files.Length < movelist.Length)
        {
            int oldLength = files.Length;
            Array.Resize(ref files, movelist.Length);
            for (int i = oldLength; i < files.Length; i++)
            {
                files[i] = (byte[])(oldLength > 1 && files[1] != null ? files[1].Clone() : new byte[0x28]);
            }
            if (oldLength < movelist.Length)
                WinFormsUtil.Alert($"Auto-Synced: Added {movelist.Length - oldLength} new move slots to match the Game Text Editor.");
        }

        // 3. ARM Engine Sync and Metronome Padding
        int maxMoveId = files.Length - 1;
        if (maxMoveId % 4 != 0) maxMoveId = (maxMoveId + 3) & ~3;
        int requiredLength = maxMoveId + 1;

        if (files.Length < requiredLength)
        {
            int oldLength = files.Length;
            Array.Resize(ref files, requiredLength);
            for (int i = oldLength; i < files.Length; i++)
            {
                files[i] = (byte[])(oldLength > 1 && files[1] != null ? files[1].Clone() : new byte[0x28]);
            }
        }
        
        if (movelist.Length < requiredLength)
        {
            int oldTextLength = movelist.Length;
            Array.Resize(ref movelist, requiredLength);
            for (int i = oldTextLength; i < requiredLength; i++)
            {
                movelist[i] = $"Dummy Pad Move {i}";
            }
        }

        EnginePatcher7.SyncEngineLimits(maxMoveId);

        // 4. Load Animation Map & Hashes
        LoadAnimationMap();
        LoadAnimationHashes();

        InitializeComponent();
        WinFormsUtil.ApplyCyberSlateTheme(this, WinFormsUtil.VisualTheme.Grey);
        RTB_MoveDesc.ReadOnly = false;
        RTB_MoveDesc.KeyDown += RTB_KeyDown;
        this.FormClosing += CloseForm;
        
        // 5. Update the session cache with the fully expanded array
        _sessionMoveData = files;
        movelist[0] = "";
        
        Setup();
        RandSettings.GetFormSettings(this, groupBox1.Controls);

        // Resiliency: Expand buffers if the move list is larger than the file count (GARC sync)
        if (entry >= 0 && entry >= files.Length)
        {
            int oldLen = files.Length;
            Array.Resize(ref files, movelist.Length);
            Array.Resize(ref _originalInfiles, movelist.Length);
            Array.Resize(ref _originalMoves, movelist.Length);
            for (int i = oldLen; i < files.Length; i++)
            {
                files[i] = (byte[])(oldLen > 1 ? files[1].Clone() : new byte[0x28]);
                _originalInfiles[i] = (byte[])files[i].Clone();
                _originalMoves[i] = (byte[])files[i].Clone();
            }
        }
        if (entry >= 0 && entry >= moveflavor.Length)
        {
            Array.Resize(ref moveflavor, movelist.Length);
            for (int i = moveflavor.Length - 1; i >= 0 && moveflavor[i] == null; i--) moveflavor[i] = "";
        }
        LoadFlagNames();
        RefreshFlagNames();
        LoadLogs();
        loading = false;
        if (CB_Move.SelectedIndex >= 0) ChangeEntry(null, null);

        // Emergency Fix for Pound (Entry 1) flags being wiped by previous initialization bugs
        if (files.Length > 1)
        {
            var pound = new Move7(files[1]);
            if (pound.Flags == 0 && pound.Power > 0) // Only if it looks like Pound and has no flags
            {
                pound.Flags = MoveFlag7.MakesContact | MoveFlag7.Protect | MoveFlag7.Mirror;
                files[1] = pound.Write();
                if (CB_Move.SelectedIndex == 0) ChangeEntry(null, null); // Refresh UI if Pound is selected
            }
        }
    }

    private void LoadLogs()
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(Main.RomFSPath) ?? "", "move_changelog.json");
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var logs = JsonSerializer.Deserialize<Dictionary<int, List<string>>>(json);
            if (logs != null) _changeLogs = logs;
            UpdateLogView();
        }
        catch { }
    }

    private void LoadFlagNames()
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(Main.RomFSPath) ?? "", FlagsPath);
            if (!File.Exists(path)) return;
            var json = File.ReadAllText(path);
            var names = JsonSerializer.Deserialize<Dictionary<int, string>>(json);
            if (names != null) customFlagNames = names;
        }
        catch { }
    }

    private void SaveFlagNames()
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(Main.RomFSPath) ?? "", FlagsPath);
            var json = JsonSerializer.Serialize(customFlagNames, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    private void SaveLogs()
    {
        try
        {
            string path = Path.Combine(Path.GetDirectoryName(Main.RomFSPath) ?? "", "move_changelog.json");
            var json = JsonSerializer.Serialize(_changeLogs, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
        }
        catch { }
    }

    private void B_VanillaLog_Click(object sender, EventArgs e)
    {
        using (OpenFileDialog ofd = new OpenFileDialog())
        {
            ofd.Title = "Select Vanilla Move GARC (a/0/1/1)";
            ofd.Filter = "All Files|*.*";
            if (ofd.ShowDialog() != DialogResult.OK) return;

            try
            {
                byte[] vanillaData = System.IO.File.ReadAllBytes(ofd.FileName);
                byte[][] vanillaMoves = null;

                bool isGarc = vanillaData.Length > 4 && 
                             ((vanillaData[0] == 'G' && vanillaData[1] == 'A' && vanillaData[2] == 'R' && vanillaData[3] == 'C') ||
                              (vanillaData[0] == 'C' && vanillaData[1] == 'R' && vanillaData[2] == 'A' && vanillaData[3] == 'G'));

                if (isGarc)
                {
                    var garc = new GARCFile(new GARC.MemGARC(vanillaData), null, ofd.FileName);
                    if (garc.Files.Length > 0)
                    {
                        vanillaMoves = Mini.UnpackMini(garc.Files[0], "WD");
                    }
                }
                else
                {
                    vanillaMoves = Mini.UnpackMini(vanillaData, "WD");
                }

                if (vanillaMoves == null || vanillaMoves.Length == 0)
                {
                    WinFormsUtil.Error("Could not unpack vanilla move data. Ensure you selected a valid move GARC.");
                    return;
                }

                _originalInfiles = vanillaMoves;
                _changeLogs.Clear();
                
                for (int i = 1; i < Math.Min(files.Length, vanillaMoves.Length); i++)
                {
                    var oldMove = new Move7(vanillaMoves[i]);
                    var newMove = new Move7(files[i]);

                    if (oldMove.Power != newMove.Power) AddBatchLog(i, "Power", oldMove.Power, newMove.Power);
                    if (oldMove.Type != newMove.Type) AddBatchLog(i, "Type", types[oldMove.Type], types[newMove.Type]);
                    if (oldMove.Accuracy != newMove.Accuracy) AddBatchLog(i, "Accuracy", oldMove.Accuracy, newMove.Accuracy);
                    if (oldMove.PP != newMove.PP) AddBatchLog(i, "PP", oldMove.PP, newMove.PP);
                    
                    string[] cats = { "Status", "Physical", "Special" };
                    if (oldMove.Category != newMove.Category) AddBatchLog(i, "Category", cats[oldMove.Category], cats[newMove.Category]);
                    if (oldMove.Priority != newMove.Priority) AddBatchLog(i, "Priority", oldMove.Priority, newMove.Priority);
                    if (oldMove.CritStage != newMove.CritStage) AddBatchLog(i, "Crit Stage", oldMove.CritStage, newMove.CritStage);
                    if (oldMove.Flinch != newMove.Flinch) AddBatchLog(i, "Flinch %", oldMove.Flinch, newMove.Flinch);
                    if (oldMove.Flags != newMove.Flags) AddBatchLog(i, "Flags", oldMove.Flags, newMove.Flags);
                    if (oldMove.ZPower != newMove.ZPower) AddBatchLog(i, "Z-Power", oldMove.ZPower, newMove.ZPower);
                }

                SaveLogs();
                _manualLogNotes = "";
                UpdateLogView();
                
                tcMain.SelectedTab = tpLog;
                WinFormsUtil.Alert("Vanilla baseline loaded!", "All existing changes between your ROM and the Vanilla baseline have been generated in the log.");
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error("Failed to process vanilla file: " + ex.Message);
            }
        }
    }

    private void AddBatchLog(int index, string property, object oldVal, object newVal)
    {
        if (!_changeLogs.ContainsKey(index)) _changeLogs[index] = new List<string>();
        _changeLogs[index].Add($"[Vanilla Compare] {property}: {oldVal} -> {newVal}");
    }

    private byte[][] _originalMoves;
    private Dictionary<int, List<string>> _changeLogs = new Dictionary<int, List<string>>();

    private void LogChange(int index, string property, object oldVal, object newVal)
    {
        if (!_changeLogs.ContainsKey(index)) _changeLogs[index] = new List<string>();
        string timestamp = DateTime.Now.ToString("HH:mm:ss");
        string entry = $"[{timestamp}] {property}: {oldVal} -> {newVal}";
        
        var last = _changeLogs[index].LastOrDefault(l => l.Contains(property));
        if (last != null && last.EndsWith(newVal.ToString())) return;

        _changeLogs[index].Add(entry);
        SaveLogs();
        
        if (string.IsNullOrEmpty(_manualLogNotes) || _manualLogNotes.StartsWith("=== SESSION"))
        {
            _manualLogNotes = ""; 
            UpdateLogView();
        }
    }

    private bool _isUpdatingLog = false;
    private string _manualLogNotes = "";
    private void OnLogChanged(object sender, EventArgs e)
    {
        if (_isUpdatingLog || rtbLog == null) return;
        _manualLogNotes = rtbLog.Text;
    }

    private void UpdateLogView()
    {
        if (rtbLog == null) return;
        _isUpdatingLog = true;
        
        if (string.IsNullOrEmpty(_manualLogNotes) || _manualLogNotes.StartsWith("=== SESSION"))
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== SESSION CHANGE LOG ===");
            sb.AppendLine("Changes are tracked until you close the editor.");
            sb.AppendLine("YOU CAN MANUALLY TYPE NOTES OR DELETE REVERTED CHANGES HERE.");
            sb.AppendLine();

            foreach (var kvp in _changeLogs.OrderBy(k => k.Key))
            {
                sb.AppendLine($"--- Move {kvp.Key:000} ({movelist[kvp.Key]}) ---");
                foreach (var log in kvp.Value)
                    sb.AppendLine(log);
                sb.AppendLine();
            }
            rtbLog.Text = sb.ToString();
            _manualLogNotes = rtbLog.Text;
        }
        else
        {
            rtbLog.Text = _manualLogNotes;
        }
        
        _isUpdatingLog = false;
    }

    private void TrackComparison(int index)
    {
        if (index < 1 || index >= files.Length || index >= _originalMoves.Length) return;
        var oldMove = new Move7(_originalMoves[index]);
        var newMove = new Move7(files[index]);

        if (oldMove.Power != newMove.Power) LogChange(index, "Power", oldMove.Power, newMove.Power);
        if (oldMove.Type != newMove.Type) LogChange(index, "Type", types[oldMove.Type], types[newMove.Type]);
        if (oldMove.Accuracy != newMove.Accuracy) LogChange(index, "Accuracy", oldMove.Accuracy, newMove.Accuracy);
        if (oldMove.PP != newMove.PP) LogChange(index, "PP", oldMove.PP, newMove.PP);
        if (oldMove.Category != newMove.Category) LogChange(index, "Category", MoveCategories[oldMove.Category], MoveCategories[newMove.Category]);
        if (oldMove.Priority != newMove.Priority) LogChange(index, "Priority", oldMove.Priority, newMove.Priority);
        if (oldMove.CritStage != newMove.CritStage) LogChange(index, "Crit Stage", oldMove.CritStage, newMove.CritStage);
        if (oldMove.Flinch != newMove.Flinch) LogChange(index, "Flinch %", oldMove.Flinch, newMove.Flinch);
        if (oldMove.Flags != newMove.Flags) LogChange(index, "Flags", oldMove.Flags, newMove.Flags);
        if (oldMove.ZPower != newMove.ZPower) LogChange(index, "Z-Power", oldMove.ZPower, newMove.ZPower);
    }

    private void LoadAnimationHashes()
    {
        var garc = Main.Config.GetGARCData("move_anim");
        if (garc == null) return;
        animFiles = garc.Files;
        if (animFiles == null) return;

        for (int i = 1; i < Math.Min(animFiles.Length, 600); i++) 
        {
            if (animFiles[i] == null || animFiles[i].Length == 0) continue;
            string hash = GetHash(animFiles[i]);
            if (!AnimHashes.ContainsKey(hash))
                AnimHashes[hash] = i;
        }
    }

    private string GetHash(byte[] data)
    {
        using var sha = System.Security.Cryptography.SHA1.Create();
        return Convert.ToBase64String(sha.ComputeHash(data));
    }

    private readonly string[] MoveCategories = ["Status", "Physical", "Special"];
    private readonly string[] StatCategories = ["None", "Attack", "Defense", "Special Attack", "Special Defense", "Speed", "Accuracy", "Evasion", "All"];

    private static readonly string[] TargetingTypes =
    [
        "Single Adjacent Ally/Foe",
        "Any Ally", "Any Adjacent Ally", "Single Adjacent Foe", "Everyone but User", "All Foes",
        "All Allies", "Self", "All Pokémon on Field", "Single Adjacent Foe (2)", "Entire Field",
        "Opponent's Field", "User's Field", "Self",
    ];

    private static readonly string[] InflictionTypes =
    [
        "None",
        "Paralyze", "Sleep", "Freeze", "Burn", "Poison",
        "Confusion", "Attract", "Capture", "Nightmare", "Curse",
        "Taunt", "Torment", "Disable", "Yawn", "Heal Block",
        "Ability Negated", "Detected", "Leech Seed", "Embargo", "Perish Song",
        "Ingrain", "Unknown 0x16", "Encore", "Mute", "Special (Use hardcoded effect)",
        "Locked into move", "Unknown 0x1B", "Unknown 0x1C", "Unknown 0x1D", "Unknown 0x1E",
        "Magnet Rise?", "Grounded", "Telekinesis", "Unknown 0x22", "Unknown 0x23",
        "Aqua Ring", "Unknown 0x25", "Unknown 0x26", "Sleepless (Uproar)", "Thrash/Petal Dance/Outrage",
        "Laser Focus", "Unknown 0x2A", "Unknown 0x2B", "Unknown 0x2C", "Unknown 0x2D",
        "Unknown 0x2E", "Unknown 0x2F", "Unknown 0x30", "Unknown 0x31", "Unknown 0x32",
        "Unknown 0x33", "Unknown 0x34", "Unknown 0x35", "Unknown 0x36", "Unknown 0x37",
        "Unknown 0x38", "Unknown 0x39", "Unknown 0x3A", "Unknown 0x3B", "Unknown 0x3C",
        "Unknown 0x3D", "Unknown 0x3E", "Unknown 0x3F", "Unknown 0x40", "Unknown 0x41",
        "Focus Punch", "Unknown 0x43", "Unknown 0x44", "Unknown 0x45", "Unknown 0x46",
        "Unknown 0x47", "Unknown 0x48", "Unknown 0x49", "Unknown 0x4A", "Unknown 0x4B",
        "Unknown 0x4C", "Unknown 0x4D", "Unknown 0x4E", "Unknown 0x4F", "Unknown 0x50",
        "Unknown 0x51", "Unknown 0x52", "Unknown 0x53", "Unknown 0x54", "Unknown 0x55",
        "Unknown 0x56", "Unknown 0x57", "Unknown 0x58", "Unknown 0x59", "Unknown 0x5A",
        "Unknown 0x5B", "Unknown 0x5C", "Unknown 0x5D", "Unknown 0x5E", "Unknown 0x5F",
        "Unknown 0x60", "Unknown 0x61", "Unknown 0x62", "Unknown 0x63", "Unknown 0x64",
        "Unknown 0x65", "Unknown 0x66", "Unknown 0x67", "Unknown 0x68", "Unknown 0x69",
        "Unknown 0x6A", "Unknown 0x6B", "Unknown 0x6C", "Beak Blast", "Shell Trap",
        "Unknown 0x6F", "Unknown 0x70", "Unknown 0x71", "Unknown 0x72", "Unknown 0x73",
        "Unknown 0x74", "Unknown 0x75", "Quick Guard, Wide Guard, Mat Block",
    ];

    private static readonly string[] MoveQualities =
    [
        "Only DMG",
        "No DMG -> Inflict Status", "No DMG -> -Target/+User Stat", "No DMG | Heal User", "DMG | Inflict Status", "No DMG | STATUS | +Target Stat",
        "DMG | -Target Stat", "DMG | +User Stat", "DMG | Absorbs DMG", "One-Hit KO", "Affects Whole Field",
        "Affect One Side of the Field", "Forces Target to Switch", "Unique Effect",
    ];

    private static readonly string[] ZMoveEffects =
    [
        "None",
        "+1 Attack",
        "+2 Attack",
        "+3 Attack",
        "+1 Defense",
        "+2 Defense",
        "+3 Defense",
        "+1 Special Attack",
        "+2 Special Attack",
        "+3 Special Attack",
        "+1 Special Defense",
        "+2 Special Defense",
        "+3 Special Defense",
        "+1 Speed",
        "+2 Speed",
        "+3 Speed",
        "+1 Accuracy",
        "+2 Accuracy",
        "+3 Accuracy",
        "+1 Evasiveness",
        "+2 Evasiveness",
        "+3 Evasiveness",
        "+1 to all (except Accuracy or Evasiveness)",
        "+2 to all (except Accuracy or Evasiveness)",
        "+3 to all (except Accuracy or Evasiveness)",
        "raises critical-hit ratio two stages",
        "resets lowered stats of the user",
        "recovers all of user's HP",
        "recovers all Hp of the Pokémon switching-in (Memento and Parting Shot)",
        "makes the user the center of attention",
        "only on Curse: recovers all HP if the user's a Ghost type, +1 Attack otherwise",
    ];

    private void LoadAnimationMap()
    {
        AnimationMap.Clear();
        if (!File.Exists(AnimationMapPath)) return;
        try
        {
            foreach (var line in File.ReadAllLines(AnimationMapPath))
            {
                var parts = line.Split('=');
                if (parts.Length == 2 && int.TryParse(parts[0], out int id) && int.TryParse(parts[1], out int anim))
                    AnimationMap[id] = anim;
            }
        }
        catch { }
    }

    private void SaveAnimationMap()
    {
        try
        {
            var lines = AnimationMap.Where(kvp => kvp.Key != kvp.Value).Select(kvp => $"{kvp.Key}={kvp.Value}");
            File.WriteAllLines(AnimationMapPath, lines);
        }
        catch { }
    }

    private void Setup()
    {
        char[] ps = ['P', 'S']; 
        for (int i = 622; i < 658; i++)
            movelist[i] += $" ({ps[i % 2]})";
        CB_Move.Items.AddRange(movelist);
        CB_Type.Items.AddRange(types);
        CB_Category.Items.AddRange(MoveCategories);
        CB_Stat1.Items.AddRange(StatCategories);
        CB_Stat2.Items.AddRange(StatCategories);
        CB_Stat3.Items.AddRange(StatCategories);
        CB_Targeting.Items.AddRange(TargetingTypes);
        CB_Quality.Items.AddRange(MoveQualities);
        CB_Inflict.Items.AddRange(InflictionTypes);
        CB_ZMove.Items.AddRange(movelist);
        RefreshFlagNames();
        CB_ZEffect.Items.AddRange(ZMoveEffects);
        var refreshtypes = Enum.GetNames(typeof(RefreshType));
        CB_AfflictRefresh.Items.AddRange(refreshtypes);

        CB_Move.Items.RemoveAt(0);
        CB_Move.SelectedIndex = 0;
    }

    private int entry = -1;

    private void ChangeEntry(object sender, EventArgs e)
    {
        if (loading) return;
        SetEntry();
        if (CB_Move.SelectedIndex < 0) return;
        
        entry = CB_Move.SelectedIndex + 1; 
        GetEntry();
        UpdateLogView();
    }

    private void GetEntry()
    {
        if (entry < 1 || entry >= files.Length) return;
        
        byte[] data = files[entry];
        
        if (data == null || data.Length < 0x24)
        {
            data = new byte[0x24];
            files[entry] = data;
        }
        
        {
            var move = new Move7(data);
            CB_Type.SelectedIndex = move.Type;
            CB_Quality.SelectedIndex = move.Quality;
            CB_Category.SelectedIndex = move.Category;
            NUD_Power.Value = move.Power;
            NUD_Accuracy.Value = move.Accuracy;
            NUD_PP.Value = move.PP;
            NUD_Priority.Value = (sbyte)move.Priority;
            NUD_HitMin.Value = move.HitMin;
            NUD_HitMax.Value = move.HitMax;
            NUD_Inflict.Value = move.InflictPercent;
            NUD_0xB.Value = (byte)move.InflictCount;
            NUD_TurnMin.Value = move.TurnMin;
            NUD_TurnMax.Value = move.TurnMax;
            NUD_CritStage.Value = move.CritStage;
            NUD_Flinch.Value = move.Flinch;
            NUD_Effect.Value = move.EffectSequence;
            NUD_Recoil.Value = (sbyte)move.Recoil;
            NUD_Heal.Value = (byte)move.Healing;
            CB_Targeting.SelectedIndex = (int)move.Target;
            CB_Stat1.SelectedIndex = move.Stat1;
            CB_Stat2.SelectedIndex = move.Stat2;
            CB_Stat3.SelectedIndex = move.Stat3;
            NUD_Stat1.Value = (sbyte)move.Stat1Stage;
            NUD_Stat2.Value = (sbyte)move.Stat2Stage;
            NUD_Stat3.Value = (sbyte)move.Stat3Stage;
            NUD_StatP1.Value = move.Stat1Percent;
            NUD_StatP2.Value = move.Stat2Percent;
            NUD_StatP3.Value = move.Stat3Percent;

            CB_ZMove.SelectedIndex = move.ZMove;
            NUD_ZPower.Value = move.ZPower;
            CB_ZEffect.SelectedIndex = move.ZEffect;

            CB_Inflict.SelectedIndex = Math.Min(move.Inflict, CB_Inflict.Items.Count - 1);
            CB_AfflictRefresh.SelectedIndex = (int)move.RefreshAfflictType;
            NUD_RefreshAfflictPercent.Value = move.RefreshAfflictPercent;

            var flags = (uint)move.Flags;
            for (int i = 0; i < CLB_Flags.Items.Count; i++)
                CLB_Flags.SetItemChecked(i, ((flags >> i) & 1) == 1);

            if (AnimationMap.TryGetValue(entry, out int anim))
            {
                NUD_AnimID.Value = anim;
            }
            else if (animFiles != null && entry < animFiles.Length)
            {
                string hash = GetHash(animFiles[entry]);
                if (AnimHashes.TryGetValue(hash, out int matchedID))
                    NUD_AnimID.Value = matchedID;
                else
                    NUD_AnimID.Value = entry;
            }
            else
            {
                NUD_AnimID.Value = entry; 
            }

            if (moveflavor != null && entry < moveflavor.Length)
                RTB_MoveDesc.Text = moveflavor[entry].Replace("\\n", Environment.NewLine);
        }
    }

    private void SetEntry()
    {
        if (entry < 1 || entry >= files.Length) return;
        TrackComparison(entry);
        
        byte[] data = files[entry];
        {
            var move = new Move7(data)
            {
                Type = CB_Type.SelectedIndex,
                Quality = CB_Quality.SelectedIndex,
                Category = CB_Category.SelectedIndex,
                Power = (int)NUD_Power.Value,
                Accuracy = (int)NUD_Accuracy.Value,
                PP = (int)NUD_PP.Value,
                Priority = (int)NUD_Priority.Value,
                HitMin = (int)NUD_HitMin.Value,
                HitMax = (int)NUD_HitMax.Value,
                Inflict = CB_Inflict.SelectedIndex,
                InflictPercent = (int)NUD_Inflict.Value,
                InflictCount = (MoveInflictDuration)NUD_0xB.Value,
                TurnMin = (int)NUD_TurnMin.Value,
                TurnMax = (int)NUD_TurnMax.Value,
                CritStage = (int)NUD_CritStage.Value,
                Flinch = (int)NUD_Flinch.Value,
                EffectSequence = (int)NUD_Effect.Value,
                Recoil = (int)NUD_Recoil.Value,
                Healing = (Heal)NUD_Heal.Value,
                Target = (MoveTarget)CB_Targeting.SelectedIndex,
                Stat1 = CB_Stat1.SelectedIndex,
                Stat2 = CB_Stat2.SelectedIndex,
                Stat3 = CB_Stat3.SelectedIndex,
                Stat1Stage = (int)NUD_Stat1.Value,
                Stat2Stage = (int)NUD_Stat2.Value,
                Stat3Stage = (int)NUD_Stat3.Value,
                Stat1Percent = (int)NUD_StatP1.Value,
                Stat2Percent = (int)NUD_StatP2.Value,
                Stat3Percent = (int)NUD_StatP3.Value,

                ZMove = CB_ZMove.SelectedIndex,
                ZPower = (int)NUD_ZPower.Value,
                ZEffect = CB_ZEffect.SelectedIndex,

                RefreshAfflictType = (RefreshType)CB_AfflictRefresh.SelectedIndex,
                RefreshAfflictPercent = (int)NUD_RefreshAfflictPercent.Value,
            };

            if (moveflavor != null && entry < moveflavor.Length)
            {
                moveflavor[entry] = RTB_MoveDesc.Text.Replace("\r\n", "\\n").Replace("\n", "\\n");
                Main.Config.SetText(TextName.MoveFlavor, moveflavor);
            }

            uint flagval = 0;
            for (int i = 0; i < CLB_Flags.Items.Count; i++)
                flagval |= CLB_Flags.GetItemChecked(i) ? 1u << i : 0;
            move.Flags = (MoveFlag7)flagval;

            int selectedAnim = (int)NUD_AnimID.Value;
            if (selectedAnim != entry)
                AnimationMap[entry] = selectedAnim;
            else
                AnimationMap.Remove(entry);
        }
        files[entry] = data;
    }

    private void CloseForm(object sender, FormClosingEventArgs e)
    {
        try { SetEntry(); } catch { }
        SaveAnimationMap();
        SaveLogs();
        SaveFlagNames();
        RandSettings.SetFormSettings(this, groupBox1.Controls);

        if (files != null)
        {
            _sessionMoveData = new byte[files.Length][];
            for (int i = 0; i < files.Length; i++)
            {
                _sessionMoveData[i] = (byte[])files[i].Clone();
            }
        }

        if (_originalInfiles != null)
        {
            for (int i = 0; i < _originalInfiles.Length; i++)
            {
                if (i < files.Length)
                {
                    _originalInfiles[i] = files[i];
                }
            }
        }
    }



    private bool ApplyARMPatch(byte[] fileData, int offset, byte[] patch)
    {
        if (offset < 0 || offset + 3 >= fileData.Length) return false;

        if (fileData[offset] == patch[0] && fileData[offset + 1] == patch[1] && 
            fileData[offset + 2] == patch[2] && fileData[offset + 3] == patch[3]) return false;

        fileData[offset] = patch[0];
        fileData[offset + 1] = patch[1];
        fileData[offset + 2] = patch[2];
        fileData[offset + 3] = patch[3];
        return true;
    }

    private byte[] GetCmpInstruction(int reg, int val)
    {
        uint uval = (uint)val;
        for (int shift = 0; shift < 32; shift += 2)
        {
            uint val_rot = ((uval << shift) & 0xFFFFFFFF) | (uval >> (32 - shift));
            if (val_rot <= 0xFF)
            {
                return new byte[] { (byte)val_rot, (byte)(shift / 2), (byte)(0x50 + reg), 0xE3 };
            }
        }
        return null;
    }

    private void B_Table_Click(object sender, EventArgs e)
    {
        var items = files.Select(z => new Move7(z));
        Clipboard.SetText(TableUtil.GetTable(items, movelist));
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_AddMove_Click(object sender, EventArgs e)
    {
        SetEntry();
        
        int newId = files.Length;
        int moveSize = 0x28; 
        
        Array.Resize(ref files, newId + 1);
        files[newId] = (byte[])(files.Length > 1 ? files[1].Clone() : new byte[moveSize]);
        
        var move = new Move7(files[newId]);
        move.Flags = MoveFlag7.None;
        files[newId] = move.Write();

        Array.Resize(ref movelist, newId + 1);
        movelist[newId] = $"New Move {newId}";
        
        if (moveflavor != null)
        {
            Array.Resize(ref moveflavor, newId + 1);
            moveflavor[newId] = "New Move Description.";
        }
        
        Array.Resize(ref _originalMoves, newId + 1);
        _originalMoves[newId] = (byte[])files[newId].Clone();

        AnimationMap[newId] = 1;

        CB_Move.Items.Add(movelist[newId]);
        CB_Move.SelectedIndex = CB_Move.Items.Count - 1;

        EnginePatcher7.SyncEngineLimits(files.Length - 1);
        _sessionMoveData = files; 

        WinFormsUtil.Alert("Move slot added and engine patched!");
    }

    private void B_ChampionsPP_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Apply Pokemon Champions PP settings to all moves?") != DialogResult.Yes) return;
        
        for (int i = 1; i < files.Length; i++)
        {
            var move = new Move7(files[i]);
            int pp = move.PP;
            
            if (pp > 0 && pp <= 5) move.PP = 8;
            else if (pp > 5 && pp <= 10) move.PP = 12;
            else if (pp > 10 && pp <= 15) move.PP = 16;
            else if (pp > 15) move.PP = 20;
            
            files[i] = move.Write();
        }
        
        entry = -1;
        if (CB_Move.SelectedIndex >= 0) { entry = CB_Move.SelectedIndex + 1; GetEntry(); }
    }

    private void B_Metronome_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Play using Metronome Mode?", "This will set the Base PP for every other Move to 0!") != DialogResult.Yes) return;

        for (int i = 0; i < CB_Move.Items.Count; i++)
        {
            CB_Move.SelectedIndex = i;
            if (CB_Move.SelectedIndex != 117 && CB_Move.SelectedIndex != 32)
                NUD_PP.Value = 0;
            if (CB_Move.SelectedIndex == 117)
                NUD_PP.Value = 40;
            if (CB_Move.SelectedIndex == 32)
                NUD_PP.Value = 1;
        }
        CB_Move.SelectedIndex = 0;
    }

    private void B_SyncBSEQ_Click(object sender, EventArgs e)
    {
        using (FolderBrowserDialog fbd = new FolderBrowserDialog())
        {
            fbd.Description = "Select your unpacked Battle Sequence (bseq) folder.";
            if (fbd.ShowDialog() != DialogResult.OK) return;

            string folderPath = fbd.SelectedPath;
            string[] folderFiles = System.IO.Directory.GetFiles(folderPath).OrderBy(f => f).ToArray();

            if (folderFiles.Length == 0) return;

            int targetCount = movelist.Length;
            if (folderFiles.Length >= targetCount) return;

            string templatePath = folderFiles.Length > 1 ? folderFiles[1] : folderFiles[0];
            byte[] templateData = System.IO.File.ReadAllBytes(templatePath);

            string extension = System.IO.Path.GetExtension(folderFiles[0]);
            bool isJson = extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
            string templateString = isJson ? System.Text.Encoding.UTF8.GetString(templateData) : null;

            for (int i = folderFiles.Length; i < targetCount; i++)
            {
                string newFileName = isJson ? (i.ToString() + extension) : (i.ToString("D3") + extension); 
                string newFilePath = System.IO.Path.Combine(folderPath, newFileName);
                
                if (isJson)
                {
                    string updatedJson = System.Text.RegularExpressions.Regex.Replace(
                        templateString, 
                        @"""Name""\s*:\s*""\d+""", 
                        $"\"Name\": \"{i}\""
                    );
                    System.IO.File.WriteAllText(newFilePath, updatedJson, System.Text.Encoding.UTF8);
                }
                else
                {
                    System.IO.File.WriteAllBytes(newFilePath, templateData);
                }
            }
        }
    }

    private void B_SaveExport_Click(object sender, EventArgs e)
    {
        try { SetEntry(); } catch { }

        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "This will repack the Move Data into the ROM (a/0/1/1).", "Continue?"))
            return;

        byte[] mini = Mini.PackMini(files, "WD");
        var gm = Main.Config.GARCMoves;
        var f = gm.Files;
        f[0] = mini;
        gm.Files = f; // Trigger repack
        gm.Save();

        EnginePatcher7.SyncEngineLimits(files.Length - 1);
        PerformAnimationSync();

        WinFormsUtil.Alert("Move GARC (a/0/1/1) repacked and saved!");
    }

    private void PerformAnimationSync()
    {
        var animPropGARC = Main.Config.GetGARCData("move_anim_prop"); 
        var animVisGARC = Main.Config.GetGARCData("move_anim");      
        
        if (animPropGARC == null || animVisGARC == null) return;

        bool propModified = false;
        bool visModified = false;

        if (animPropGARC.Files.Length < files.Length)
        {
            var pFiles = animPropGARC.Files.ToList();
            byte[] pTemplate = pFiles.Count > 1 ? pFiles[1] : new byte[0];
            while (pFiles.Count < files.Length) pFiles.Add((byte[])pTemplate.Clone());
            animPropGARC.Files = pFiles.ToArray();
            propModified = true;
        }

        if (animVisGARC.Files.Length < files.Length)
        {
            var vFiles = animVisGARC.Files.ToList();
            byte[] vTemplate = vFiles.Count > 1 ? vFiles[1] : new byte[0];
            while (vFiles.Count < files.Length) vFiles.Add((byte[])vTemplate.Clone());
            animVisGARC.Files = vFiles.ToArray();
            visModified = true;
        }

        int maxProp = animPropGARC.Files.Length;
        int maxVis = animVisGARC.Files.Length;

        for (int i = 1; i < files.Length; i++)
        {
            int animID = AnimationMap.ContainsKey(i) ? AnimationMap[i] : i;
            
            if (animID < maxProp && i < maxProp)
            {
                var fileI = animPropGARC.Files[i] ?? new byte[0];
                var fileAnimID = animPropGARC.Files[animID] ?? new byte[0];
                if (!fileI.SequenceEqual(fileAnimID))
                {
                    animPropGARC.Files[i] = (byte[])fileAnimID.Clone();
                    propModified = true;
                }
            }

            if (animID < maxVis && i < maxVis)
            {
                var fileI = animVisGARC.Files[i] ?? new byte[0];
                var fileAnimID = animVisGARC.Files[animID] ?? new byte[0];
                if (!fileI.SequenceEqual(fileAnimID))
                {
                    animVisGARC.Files[i] = (byte[])fileAnimID.Clone();
                    visModified = true;
                }
            }
        }

        if (propModified) animPropGARC.Save();
        if (visModified) animVisGARC.Save();
        if (propModified || visModified) SaveAnimationMap();
    }

    private void B_SyncAnim_Click(object sender, EventArgs e)
    {
        PerformAnimationSync();
        WinFormsUtil.Alert("Animation syncing complete!");
    }

    private void B_CopyAnim_Click(object sender, EventArgs e)
    {
        if (entry < 1) return;
        AnimationMap[entry] = (int)NUD_CopyAnim.Value;
        NUD_AnimID.Value = NUD_CopyAnim.Value;
        PerformAnimationSync();
    }

    private void B_ImportTxt_Click(object sender, EventArgs e)
    {
        var ofd = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        string[] lines = System.IO.File.ReadAllLines(ofd.FileName);
        int currentId = -1;
        int updated = 0;
        string[] categories = ["Status", "Physical", "Special"];

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.StartsWith("==="))
            {
                string inner = line.Trim(' ', '=');
                int colonIdx = inner.IndexOf(':');
                if (colonIdx > 0 && int.TryParse(inner.Substring(0, colonIdx).Trim(), out int id))
                    currentId = id;
                continue;
            }
            if (currentId < 1 || currentId >= files.Length) continue;

            byte[] data = files[currentId];
            if (data == null || data.Length < 0x24) continue;

            int kvSep = line.IndexOf(':');
            if (kvSep < 0) continue;
            string key = line.Substring(0, kvSep).Trim();
            string val = line.Substring(kvSep + 1).Trim();

            switch (key)
            {
                case "Type":
                    int ti = Array.IndexOf(types, val);
                    if (ti >= 0) new Move7(data).Type = ti;
                    break;
                case "Category":
                    int ci = Array.IndexOf(categories, val);
                    if (ci >= 0) new Move7(data).Category = ci;
                    break;
                case "Power": if (byte.TryParse(val, out byte pw)) new Move7(data).Power = pw; break;
                case "Accuracy": if (byte.TryParse(val, out byte ac)) new Move7(data).Accuracy = ac; break;
                case "PP": if (byte.TryParse(val, out byte pp)) new Move7(data).PP = pp; break;
                case "Priority": if (int.TryParse(val, out int pr)) new Move7(data).Priority = pr; break;
                case "CritStage": if (byte.TryParse(val, out byte cs)) new Move7(data).CritStage = cs; break;
                case "Flinch": if (byte.TryParse(val, out byte fl)) new Move7(data).Flinch = fl; break;
                case "Effect": if (ushort.TryParse(val, out ushort ef)) new Move7(data).EffectSequence = ef; break;
                case "Recoil": if (int.TryParse(val, out int rc)) new Move7(data).Recoil = rc; break;
                case "Heal": if (byte.TryParse(val, out byte hl)) new Move7(data).Healing = (Heal)hl; break;
                case "Targeting": if (byte.TryParse(val, out byte tg)) new Move7(data).Target = (MoveTarget)tg; break;
                case "Inflict": if (ushort.TryParse(val, out ushort inf)) new Move7(data).Inflict = inf; break;
                case "TurnMin": if (byte.TryParse(val, out byte tmn)) new Move7(data).TurnMin = tmn; break;
                case "TurnMax": if (byte.TryParse(val, out byte tmx)) new Move7(data).TurnMax = tmx; break;
                case "Quality": if (byte.TryParse(val, out byte ql)) new Move7(data).Quality = ql; break;
                case "0xB": if (byte.TryParse(val, out byte xb)) new Move7(data).InflictCount = (MoveInflictDuration)xb; break;
                case "ZMove": if (ushort.TryParse(val, out ushort zm)) new Move7(data).ZMove = zm; break;
                case "ZPower": if (byte.TryParse(val, out byte zp)) new Move7(data).ZPower = zp; break;
                case "ZEffect": if (byte.TryParse(val, out byte ze)) new Move7(data).ZEffect = ze; break;
            }
            updated++;
        }
        entry = -1;
        if (CB_Move.SelectedIndex >= 0) { entry = CB_Move.SelectedIndex + 1; GetEntry(); }
    }

    private void B_RandAll_Click(object sender, EventArgs e)
    {
        if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomize moves?") != DialogResult.Yes) return;
        
        var rand = new Random();
        for (int i = 1; i < files.Length; i++)
        {
            var move = new Move7(files[i]);
            if (CHK_Type.Checked) move.Type = (byte)rand.Next(types.Length);
            if (CHK_Category.Checked) move.Category = (byte)rand.Next(3);
            files[i] = move.Write();
        }
        
        entry = -1;
        if (CB_Move.SelectedIndex >= 0) { entry = CB_Move.SelectedIndex + 1; GetEntry(); }
    }

    private Dictionary<int, string> customFlagNames = new();
    private const string FlagsPath = "move_flags.json";

    private void RefreshFlagNames()
    {
        var flagnames = Enum.GetNames(typeof(MoveFlag7)).Skip(1).ToArray();
        for (int i = 0; i < flagnames.Length; i++)
        {
            if (customFlagNames.TryGetValue(i, out string custom))
                flagnames[i] = custom;
        }
        CLB_Flags.Items.Clear();
        CLB_Flags.Items.AddRange(flagnames);
    }

    private void B_RenameFlags_Click(object sender, EventArgs e)
    {
        int sel = CLB_Flags.SelectedIndex;
        if (sel < 0 || sel < 17) return;
        
        string current = CLB_Flags.Items[sel].ToString();
        string input = WinFormsUtil.PromptInput("Rename Flag", "Enter name:", current);
        if (!string.IsNullOrWhiteSpace(input))
        {
            customFlagNames[sel] = input;
            RefreshFlagNames();
            SaveFlagNames();
        }
    }

    private void B_ExportTxt_Click(object sender, EventArgs e)
    {
        var sfd = new SaveFileDialog { FileName = "Moves.txt", Filter = "Text File|*.txt" };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        var sb = new StringBuilder();
        string[] categories = ["Status", "Physical", "Special"];
        for (int i = 1; i < files.Length; i++)
        {
            var move = new Move7(files[i]);
            sb.AppendLine($"=== {i:000}: {movelist[i]} ===");
            sb.AppendLine($"Type: {types[move.Type]}");
            sb.AppendLine($"Category: {categories[move.Category]}");
            sb.AppendLine($"Power: {move.Power}");
            sb.AppendLine($"Accuracy: {move.Accuracy}");
            sb.AppendLine($"PP: {move.PP}");
            sb.AppendLine();
        }
        File.WriteAllText(sfd.FileName, sb.ToString());
    }
// Temporary storage for the copied move data
    private byte[] copiedMoveData = null;

    private void B_CopyData_Click(object sender, EventArgs e)
    {
        if (entry < 1 || entry >= files.Length) return;

        // 1. Force the UI to save any unsaved typing to the underlying file array
        SetEntry();
        
        // 2. Clone the exact byte array for the current move into the clipboard
        copiedMoveData = (byte[])files[entry].Clone();
        
        WinFormsUtil.Alert("Move data copied!");
    }

    private void B_PasteData_Click(object sender, EventArgs e)
    {
        if (copiedMoveData == null)
        {
            WinFormsUtil.Alert("No move data copied yet!");
            return;
        }
        if (entry < 1 || entry >= files.Length) return;

        // 1. Overwrite the current move's byte array with the copied data
        files[entry] = (byte[])copiedMoveData.Clone();
        
        // 2. Force the UI to reload and display the newly pasted byte array
        GetEntry();
        
        WinFormsUtil.Alert("Move data pasted!");
    }

    private void RTB_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Shift && e.KeyCode == Keys.Enter)
        {
            var rtb = (RichTextBox)sender;
            int selectionStart = rtb.SelectionStart;
            rtb.SelectedText = Environment.NewLine;
            rtb.SelectionStart = selectionStart + Environment.NewLine.Length;
            rtb.SelectionLength = 0;
            e.SuppressKeyPress = true;
            e.Handled = true;
        }
    }
}