using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Windows.Forms;

using pk3DS.Core;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class MegaEvoEditor7 : Form
{
    private readonly byte[][] files;
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private readonly GroupBox[] groupbox_spec;
    private readonly ComboBox[] forme_spec;
    private readonly ComboBox[] item_spec;
    private readonly CheckBox[] checkbox_spec;
    private readonly PictureBox[][] picturebox_spec;
    private bool loaded;
    private readonly string[][] AltForms;
    private int entry = -1;
    private MegaEvolutions me;
    private readonly int[] baseForms, formVal;

    public MegaEvoEditor7(byte[][] infiles)
    {
        files = infiles;
        InitializeComponent();
        CB_Species.DisplayMember = "Text";
        CB_Species.ValueMember = "Value";

        string[] species = Main.Config.GetText(TextName.SpeciesNames);
        AltForms = Main.Config.Personal.GetFormList(species, Main.Config.MaxSpeciesID);
        
        string[] specieslist_with_forms = Main.Config.Personal.GetPersonalEntryList(AltForms, species, Main.Config.MaxSpeciesID, out baseForms, out formVal);

        if (specieslist_with_forms.Length > 0) specieslist_with_forms[0] = "(None)"; 
        if (itemlist.Length > 0) itemlist[0] = "(None)";

        int personalCount = specieslist_with_forms.Length;
        int fileCount = files.Length;
        
        // Dynamic List: Populates up to your maximum file count (Supports all 1148+ custom forms)
        var availableList = new List<ComboItem>();
        for (int i = 0; i < fileCount; i++)
        {
            string name = i < personalCount ? specieslist_with_forms[i] : $"Species {i}";
            availableList.Add(new ComboItem { Text = name, Value = i });
        }

        CB_Species.DataSource = availableList;

        groupbox_spec = new[] { GB_MEvo1, GB_MEvo2 };
        forme_spec = new[] { CB_Forme1, CB_Forme2 };
        item_spec = new[] { CB_Item1, CB_Item2 };
        checkbox_spec = new[] { CHK_MEvo1, CHK_MEvo2 };
        picturebox_spec = new[] { new[] { PB_S1, PB_M1 }, new[] { PB_S2, PB_M2 } };

        var itemComboList = new List<ComboItem>();
        for (int i = 0; i < itemlist.Length; i++)
            itemComboList.Add(new ComboItem { Text = itemlist[i], Value = i });

        foreach (ComboBox cb in item_spec)
        {
            cb.DisplayMember = "Text";
            cb.ValueMember = "Value";
            cb.DataSource = new List<ComboItem>(itemComboList);
        }

        loaded = true;
        
        // Safely set index without triggering bad saves during initialization
        if (CB_Species.Items.Count > 1)
            CB_Species.SelectedIndex = 1; 
        else if (CB_Species.Items.Count > 0)
            CB_Species.SelectedIndex = 0;
    }

    // Helper: Populates the "Into" dropdown dynamically with the forms of the Base Species
    private void SetForms(int species, ComboBox cb)
    {
        cb.Items.Clear();
        cb.Items.Add("Base Form");
        if (species < AltForms.Length && AltForms[species] != null)
        {
            for (int i = 1; i < AltForms[species].Length; i++)
                cb.Items.Add(AltForms[species][i]);
        }
    }

    private void GetEntry()
    {
        if (!loaded || entry < 0 || entry >= files.Length) return;
        
        if (Main.Config.ORAS && entry == 384) 
            WinFormsUtil.Alert("Rayquaza is special and uses a different activator for its evolution. If it knows Dragon Ascent, it can Mega Evolve", "Don't edit its evolution table if you want to keep this functionality.");

        byte[] data = files[entry];
        me = new MegaEvolutions(data);

        // Find the absolute base species even if you are editing an alternate form slot
        int baseSp = entry <= Main.Config.MaxSpeciesID ? entry : (entry < baseForms.Length ? baseForms[entry] : entry);

        for (int i = 0; i < 2; i++)
        {
            checkbox_spec[i].Checked = me.Method[i] == 1;
            
            // Set Item Safely
            SetComboValue(item_spec[i], (int)me.Argument[i]);
            
            // Generate the list of Forms for THIS specific Pokemon
            SetForms(baseSp, forme_spec[i]);
            
            // Set Form Index Safely to map to the correct text
            int formIdx = (int)me.Form[i];
            if (formIdx >= 0 && formIdx < forme_spec[i].Items.Count)
                forme_spec[i].SelectedIndex = formIdx;
            else if (forme_spec[i].Items.Count > 0)
                forme_spec[i].SelectedIndex = 0;
        }
        UpdateSprite(null, null);
    }

    private void SetComboValue(ComboBox cb, int id)
    {
        for (int i = 0; i < cb.Items.Count; i++)
        {
            if (cb.Items[i] is ComboItem ci && (int)ci.Value == id)
            {
                cb.SelectedIndex = i;
                return;
            }
        }
        if (cb.Items.Count > 0) cb.SelectedIndex = 0;
    }

    private void SetEntry()
    {
        // Safety guard: Prevents wiping data if 'me' hasn't loaded yet
        if (entry < 1 || me == null) return; 
        if (Main.Config.ORAS && entry == 384) return; // Do not overwrite Rayquaza
        
        for (int i = 0; i < 2; i++)
        {
            me.Method[i] = checkbox_spec[i].Checked ? (byte)1 : (byte)0;
            
            if (item_spec[i].SelectedValue != null)
                me.Argument[i] = (ushort)(int)item_spec[i].SelectedValue;
            
            // Saves the relative Form Index, preventing the "Bulbasaur" data corruption
            if (forme_spec[i].SelectedIndex >= 0)
                me.Form[i] = (ushort)forme_spec[i].SelectedIndex;
        }
        files[entry] = me.Write();
    }

    private void UpdateSprite(object sender, EventArgs e)
    {
        if (entry < 0 || entry >= files.Length) return;

        int baseSp = entry <= Main.Config.MaxSpeciesID ? entry : (entry < baseForms.Length ? baseForms[entry] : entry);
        int fVal = entry <= Main.Config.MaxSpeciesID ? 0 : (entry < formVal.Length ? formVal[entry] : 0);
        
        System.Drawing.Bitmap src = WinFormsUtil.GetSprite(baseSp, fVal, 0, 0, Main.Config);
        PB_S1.Image = PB_S2.Image = src;

        for (int i = 0; i < 2; i++)
        {
            if (!checkbox_spec[i].Checked)
            {
                picturebox_spec[i][1].Image = null;
                continue;
            }

            int formIdx = forme_spec[i].SelectedIndex;
            if (formIdx < 0) formIdx = 0;
            
            WinFormsUtil.SetImage(picturebox_spec[i][1], WinFormsUtil.GetSprite(baseSp, formIdx, 0, 0, Main.Config));
        }
    }

    private void B_Dump_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Dump all Mega Evolution data to a Text File?"))
            return;
            
        StringBuilder sb = new StringBuilder();
        
        for (int i = 1; i < files.Length; i++)
        {
            var temp_me = new MegaEvolutions(files[i]);
            bool headered = false;

            for (int j = 0; j < 2; j++)
            {
                if (temp_me.Method[j] != 1) continue;
                
                if (!headered)
                {
                    sb.AppendLine("======");
                    string name = CB_Species.Items.Cast<ComboItem>().FirstOrDefault(z => (int)z.Value == i)?.Text ?? $"Species {i}";
                    sb.AppendLine($"{i} {name}");
                    sb.AppendLine("======");
                    headered = true;
                }
                
                int baseSp = i <= Main.Config.MaxSpeciesID ? i : (i < baseForms.Length ? baseForms[i] : i);
                string formName = "Unknown Form";
                if (baseSp < AltForms.Length && AltForms[baseSp] != null)
                {
                    if (temp_me.Form[j] < AltForms[baseSp].Length)
                        formName = temp_me.Form[j] == 0 ? "Base Form" : AltForms[baseSp][temp_me.Form[j]];
                }
                else if (temp_me.Form[j] == 0) formName = "Base Form";

                string itemName = "(None)";
                if (temp_me.Argument[j] < itemlist.Length) itemName = itemlist[temp_me.Argument[j]];

                sb.AppendLine($"Can Mega Evolve into {formName} if its held item is {itemName}.");
            }
            if (headered) sb.AppendLine();
        }

        var sfd = new SaveFileDialog { FileName = "Mega_Data.txt", Filter = "Text File|*.txt" };
        SystemSounds.Asterisk.Play();
        if (sfd.ShowDialog() == DialogResult.OK)
            File.WriteAllText(sfd.FileName, sb.ToString(), Encoding.Unicode);
    }

    private void CHK_Changed(object sender, EventArgs e)
    {
        for (int i = 0; i < groupbox_spec.Length; i++)
        {
            groupbox_spec[i].Enabled = checkbox_spec[i].Checked;
        }
        UpdateSprite(null, null);
    }

    private void ChangeIndex(object sender, EventArgs e)
    {
        if (!loaded) return;
        SetEntry();
        
        object val = CB_Species.SelectedValue;
        if (val == null) return;
        
        entry = (int)val;
        GetEntry();
    }

    private void Update_PBs(object sender, EventArgs e)
    {
        if (!loaded) return;
        UpdateSprite(null, null);
    }

    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetEntry();
    }
}