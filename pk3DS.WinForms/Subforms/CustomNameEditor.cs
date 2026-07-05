using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows.Forms;
using pk3DS.Core;

namespace pk3DS.WinForms;

/// <summary>
/// Allows users to assign custom display names to Pokémon and their forms
/// within the editor only. These names are NOT written back to the ROM.
/// They are persisted in CustomNames.json next to the executable.
/// </summary>
public partial class CustomNameEditor : Form
{
    private static readonly string CustomNamesFile = Path.Combine(Application.StartupPath, "CustomNames.json");

    // Entry index → custom display name
    private Dictionary<int, string> customNames = new();

    public CustomNameEditor()
    {
        InitializeComponent();
        LoadNames();
        PopulateList();
    }

    // ── Persistence ─────────────────────────────────────────────────────────

    public static Dictionary<int, string> LoadCustomNames()
    {
        if (!File.Exists(CustomNamesFile))
            return new Dictionary<int, string>();
        try
        {
            var json = File.ReadAllText(CustomNamesFile);
            return JsonSerializer.Deserialize<Dictionary<int, string>>(json) ?? new();
        }
        catch { return new(); }
    }

    private void LoadNames()
    {
        customNames = LoadCustomNames();
    }

    private void SaveNames()
    {
        try
        {
            var json = JsonSerializer.Serialize(customNames, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(CustomNamesFile, json);
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Failed to save custom names.", ex.Message);
        }
    }

    // ── List Population ──────────────────────────────────────────────────────

    private void PopulateList()
    {
        if (Main.Config == null) return;

        var allNames = GetAllEntryNames();
        LB_Entries.Items.Clear();
        for (int i = 0; i < allNames.Length; i++)
        {
            string custom = customNames.TryGetValue(i, out string cn) ? cn : string.Empty;
            LB_Entries.Items.Add(FormatListItem(i, allNames[i], custom));
        }
        if (LB_Entries.Items.Count > 0)
            LB_Entries.SelectedIndex = 0;
    }

    private static string FormatListItem(int index, string baseName, string custom)
    {
        string display = string.IsNullOrWhiteSpace(custom) ? baseName : $"{baseName}  →  {custom}";
        return $"{index:000} {display}";
    }

    private static string[] GetAllEntryNames()
    {
        if (Main.Config == null) return [];
        string[] speciesNames = Main.Config.GetText(TextName.SpeciesNames);
        string[] names = Main.Config.Personal.GetPersonalEntryList(
            Main.Config.Personal.GetFormList(speciesNames, Main.Config.MaxSpeciesID),
            speciesNames,
            Main.Config.MaxSpeciesID,
            out _,
            out _);
        return names;
    }

    // ── Events ───────────────────────────────────────────────────────────────

    private void LB_Entries_SelectedIndexChanged(object sender, EventArgs e)
    {
        int idx = LB_Entries.SelectedIndex;
        if (idx < 0) return;

        bool hasCustom = customNames.TryGetValue(idx, out string existing);
        TB_CustomName.Text = hasCustom ? existing : string.Empty;
        B_Remove.Enabled = hasCustom;
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        int idx = LB_Entries.SelectedIndex;
        if (idx < 0) return;

        string name = TB_CustomName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            WinFormsUtil.Alert("Please enter a custom name, or use 'Remove' to clear the existing one.");
            return;
        }

        customNames[idx] = name;
        SaveNames();
        RefreshListItem(idx);
        B_Remove.Enabled = true;
    }

    private void B_Remove_Click(object sender, EventArgs e)
    {
        int idx = LB_Entries.SelectedIndex;
        if (idx < 0) return;

        customNames.Remove(idx);
        SaveNames();
        TB_CustomName.Text = string.Empty;
        RefreshListItem(idx);
        B_Remove.Enabled = false;
    }

    private void TB_CustomName_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            B_Save_Click(sender, e);
            e.SuppressKeyPress = true;
        }
    }

    private void RefreshListItem(int idx)
    {
        var allNames = GetAllEntryNames();
        if (idx >= allNames.Length) return;
        string custom = customNames.TryGetValue(idx, out string cn) ? cn : string.Empty;
        LB_Entries.Items[idx] = FormatListItem(idx, allNames[idx], custom);
    }
}
