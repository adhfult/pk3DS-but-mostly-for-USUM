using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
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
        if (Main.Config == null) return;

        string[] speciesList = Main.Config.GetText(TextName.SpeciesNames);
        CB_Species.Items.Clear();
        foreach (var species in speciesList)
        {
            CB_Species.Items.Add(species);
        }

        if (CB_Species.Items.Count > 0)
            CB_Species.SelectedIndex = 1; // Default to Bulbasaur
    }

    private void UpdateSprite(object sender, EventArgs e)
    {
        if (CB_Species.SelectedIndex <= 0) return;

        int species = CB_Species.SelectedIndex;
        int form = CB_Form.SelectedIndex < 0 ? 0 : CB_Form.SelectedIndex;
        int gender = 0;

        // If species changed and triggered this, update forms dropdown if necessary
        if (sender == CB_Species)
        {
            UpdateFormList(species);
            form = 0;
        }

        PB_Sprite.Image = WinFormsUtil.ScaleImage(WinFormsUtil.GetSprite(species, form, gender, 0, Main.Config), 2);
        
        string filename = WinFormsUtil.GetResourceStringSprite(species, form, gender, Main.Config?.Generation ?? 7) + ".png";
        string fullPath = Path.Combine(CustomSpriteDir, filename);

        L_Status.Text = File.Exists(fullPath) ? "Custom Sprite Active" : "Default Sprite";
        B_Remove.Enabled = File.Exists(fullPath);
    }

    private void UpdateFormList(int species)
    {
        CB_Form.SelectedIndexChanged -= UpdateSprite;
        CB_Form.Items.Clear();

        if (Main.SpeciesStat != null && species < Main.SpeciesStat.Length)
        {
            var pkm = Main.SpeciesStat[species];
            int formCount = pkm.FormeCount;
            if (formCount <= 0) formCount = 1;

            for (int i = 0; i < formCount; i++)
            {
                CB_Form.Items.Add($"Form {i}");
            }
        }
        else
        {
            CB_Form.Items.Add("Form 0");
        }

        CB_Form.SelectedIndex = 0;
        CB_Form.SelectedIndexChanged += UpdateSprite;
    }

    private void B_Upload_Click(object sender, EventArgs e)
    {
        if (CB_Species.SelectedIndex <= 0) return;

        using (OpenFileDialog ofd = new OpenFileDialog())
        {
            ofd.Filter = "PNG Files (*.png)|*.png|All Files (*.*)|*.*";
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

                    string filename = WinFormsUtil.GetResourceStringSprite(species, form, gender, Main.Config?.Generation ?? 7) + ".png";
                    string destPath = Path.Combine(CustomSpriteDir, filename);

                    File.Copy(ofd.FileName, destPath, true);
                    
                    // Force refresh sprite
                    UpdateSprite(null, EventArgs.Empty);
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
        if (CB_Species.SelectedIndex <= 0) return;

        int species = CB_Species.SelectedIndex;
        int form = CB_Form.SelectedIndex < 0 ? 0 : CB_Form.SelectedIndex;
        int gender = 0;

        string filename = WinFormsUtil.GetResourceStringSprite(species, form, gender, Main.Config?.Generation ?? 7) + ".png";
        string destPath = Path.Combine(CustomSpriteDir, filename);

        if (File.Exists(destPath))
        {
            try
            {
                File.Delete(destPath);
                UpdateSprite(null, EventArgs.Empty);
                WinFormsUtil.Alert("Custom sprite removed.");
            }
            catch (Exception ex)
            {
                WinFormsUtil.Error("Failed to remove custom sprite.", ex.Message);
            }
        }
    }
}
