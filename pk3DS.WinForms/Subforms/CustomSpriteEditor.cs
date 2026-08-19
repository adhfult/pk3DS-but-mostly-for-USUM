using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Collections.Generic;
using pk3DS.Core;

namespace pk3DS.WinForms;

public partial class CustomSpriteEditor : Form
{
    private string CustomSpriteDir => Path.Combine(Application.StartupPath, "CustomSprites");

    public CustomSpriteEditor()
    {
        InitializeComponent();
        SetupControls();
        LoadPokemon();
    }

    private void SetupControls()
    {
        CB_Species.SelectedIndexChanged += UpdateSprite;
        CB_Form.SelectedIndexChanged += UpdateSprite;

        B_Upload.Click += B_Upload_Click;
        B_Remove.Click += B_Remove_Click;
    }

    private void LoadPokemon()
    {
        string[] speciesList = null;
        if (Main.Config != null)
        {
            speciesList = Main.Config.GetText(TextName.SpeciesNames);
        }

        if (speciesList == null || speciesList.Length == 0)
        {
            // Fallback list if no ROM loaded
            speciesList = WinFormsUtil.GetSimpleStringList("species_en");
        }

        CB_Species.Items.Clear();
        if (speciesList != null)
        {
            foreach (var species in speciesList)
            {
                CB_Species.Items.Add(species);
            }
        }

        if (CB_Species.Items.Count > 1)
            CB_Species.SelectedIndex = 1; // Default to Bulbasaur
        else if (CB_Species.Items.Count > 0)
            CB_Species.SelectedIndex = 0;
    }

    private void UpdateSprite(object sender, EventArgs e)
    {
        if (CB_Species.SelectedIndex < 0) return;

        int species = CB_Species.SelectedIndex;
        int form = CB_Form.SelectedIndex < 0 ? 0 : CB_Form.SelectedIndex;
        int gender = 0;

        // If species changed and triggered this, update forms dropdown
        if (sender == CB_Species)
        {
            UpdateFormList(species);
            form = 0;
        }

        if (PB_Sprite.Image != null)
        {
            var old = PB_Sprite.Image;
            PB_Sprite.Image = null;
            old.Dispose();
        }

        var rawBmp = WinFormsUtil.GetSprite(species, form, gender, 0, Main.Config);
        if (rawBmp != null)
        {
            PB_Sprite.Image = WinFormsUtil.ScaleImage(rawBmp, 2);
        }

        int formIdx = species;
        if (form > 0 && Main.Config?.Personal != null && species < Main.Config.Personal.Table.Length)
        {
            try
            {
                int pIdx = Main.Config.Personal.GetFormIndex(species, form);
                if (pIdx > 0 && pIdx < Main.Config.Personal.Table.Length)
                    formIdx = pIdx;
            }
            catch { }
        }

        string filename = WinFormsUtil.GetResourceStringSprite(species, form, gender, Main.Config?.Generation ?? 7) + ".png";
        string path1 = Path.Combine(CustomSpriteDir, filename);
        string path2 = formIdx > species ? Path.Combine(CustomSpriteDir, $"_{formIdx}.png") : null;

        bool active = File.Exists(path1) || (path2 != null && File.Exists(path2));

        L_Status.Text = active ? "Custom Sprite Active" : "Default Sprite";
        B_Remove.Enabled = active;
    }

    private void UpdateFormList(int species)
    {
        CB_Form.SelectedIndexChanged -= UpdateSprite;
        CB_Form.Items.Clear();

        int formCount = 1;

        if (Main.Config?.Personal != null && species < Main.Config.Personal.Table.Length)
        {
            formCount = Main.Config.Personal.Table[species].FormeCount;
        }
        else if (Main.SpeciesStat != null && species < Main.SpeciesStat.Length)
        {
            formCount = Main.SpeciesStat[species].FormeCount;
        }

        if (formCount <= 0) formCount = 1;

        for (int i = 0; i < formCount; i++)
        {
            CB_Form.Items.Add($"Form {i}");
        }

        CB_Form.SelectedIndex = 0;
        CB_Form.SelectedIndexChanged += UpdateSprite;
    }

    private void B_Upload_Click(object sender, EventArgs e)
    {
        if (CB_Species.SelectedIndex < 0) return;

        using (OpenFileDialog ofd = new OpenFileDialog())
        {
            ofd.Filter = "PNG Files (*.png)|*.png|All Files (*.*)|*.*";
            ofd.Title = "Select Custom Sprite PNG";
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (!Directory.Exists(CustomSpriteDir))
                    {
                        Directory.CreateDirectory(CustomSpriteDir);
                    }

                    int species = CB_Species.SelectedIndex;
                    int form = CB_Form.SelectedIndex < 0 ? 0 : CB_Form.SelectedIndex;
                    int gender = 0;

                    int formIdx = species;
                    if (form > 0 && Main.Config?.Personal != null && species < Main.Config.Personal.Table.Length)
                    {
                        try
                        {
                            int pIdx = Main.Config.Personal.GetFormIndex(species, form);
                            if (pIdx > 0 && pIdx < Main.Config.Personal.Table.Length)
                                formIdx = pIdx;
                        }
                        catch { }
                    }

                    string filename = WinFormsUtil.GetResourceStringSprite(species, form, gender, Main.Config?.Generation ?? 7) + ".png";
                    string destPath = Path.Combine(CustomSpriteDir, filename);

                    File.Copy(ofd.FileName, destPath, true);

                    if (formIdx > species)
                    {
                        string formPath = Path.Combine(CustomSpriteDir, $"_{formIdx}.png");
                        File.Copy(ofd.FileName, formPath, true);
                    }

                    // Force refresh sprite
                    UpdateSprite(null, EventArgs.Empty);
                    WinFormsUtil.Alert("Custom sprite saved successfully!");
                }
                catch (Exception ex)
                {
                    WinFormsUtil.Error("Failed to upload sprite.", ex.Message);
                }
            }
        }
    }

    private void B_Remove_Click(object sender, EventArgs e)
    {
        if (CB_Species.SelectedIndex < 0) return;

        int species = CB_Species.SelectedIndex;
        int form = CB_Form.SelectedIndex < 0 ? 0 : CB_Form.SelectedIndex;
        int gender = 0;

        int formIdx = species;
        if (form > 0 && Main.Config?.Personal != null && species < Main.Config.Personal.Table.Length)
        {
            try
            {
                int pIdx = Main.Config.Personal.GetFormIndex(species, form);
                if (pIdx > 0 && pIdx < Main.Config.Personal.Table.Length)
                    formIdx = pIdx;
            }
            catch { }
        }

        string filename = WinFormsUtil.GetResourceStringSprite(species, form, gender, Main.Config?.Generation ?? 7) + ".png";
        string destPath = Path.Combine(CustomSpriteDir, filename);
        string formPath = formIdx > species ? Path.Combine(CustomSpriteDir, $"_{formIdx}.png") : null;

        bool removed = false;
        try
        {
            if (File.Exists(destPath)) { File.Delete(destPath); removed = true; }
            if (formPath != null && File.Exists(formPath)) { File.Delete(formPath); removed = true; }

            if (removed)
            {
                UpdateSprite(null, EventArgs.Empty);
                WinFormsUtil.Alert("Custom sprite removed.");
            }
        }
        catch (Exception ex)
        {
            WinFormsUtil.Error("Failed to remove custom sprite.", ex.Message);
        }
    }
}
