using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;

using pk3DS.Core;
using pk3DS.Core.Structures;

namespace pk3DS.WinForms;

public partial class ItemEditor7 : Form
{
    public ItemEditor7(byte[][] infiles)
    {
        files = infiles;
        itemlist[0] = "";

        InitializeComponent();
        RTB.ReadOnly = false;
        RTB.KeyDown += RTB_KeyDown;
        this.FormClosing += Form_Closing;
        Setup();
        ApplyGridTheme(WinFormsUtil.CurrentTheme == WinFormsUtil.VisualTheme.Dark);
        WinFormsUtil.ApplyCyberSlateTheme(this, WinFormsUtil.VisualTheme.Grey);
    }

    private void ApplyGridTheme(bool dark)
    {
        Grid.ViewBackColor = dark ? Color.FromArgb(30, 30, 35) : Color.FromArgb(50, 50, 60);
        Grid.ViewForeColor = Color.White;
        Grid.LineColor = dark ? Color.FromArgb(60, 60, 75) : Color.FromArgb(90, 90, 100);
        Grid.CategoryForeColor = Color.Gold;
        Grid.HelpBackColor = Grid.ViewBackColor;
        Grid.HelpForeColor = Color.LightCyan;
        Grid.CommandsBackColor = Grid.ViewBackColor;
        Grid.CommandsForeColor = Color.White;
        Grid.SelectedItemWithFocusBackColor = Color.FromArgb(80, 100, 150);
    }

    private byte[][] files;
    public byte[][] Files => files;
    private readonly string[] itemlist = Main.Config.GetText(TextName.ItemNames);
    private string[] itemflavor = Main.Config.GetText(TextName.ItemFlavor);

    private void Setup()
    {
        CB_Item.Items.AddRange(itemlist);
        CB_Item.SelectedIndex = 1;
    }

    /// <summary>
    /// Adds item slots past the end of the table, the way Add New Move Slot adds moves.
    /// </summary>
    private void B_AddItem_Click(object sender, EventArgs e)
    {
        string input = WinFormsUtil.PromptInput("Add Item Slots",
            "How many new item slots?", "1");
        if (string.IsNullOrWhiteSpace(input)) return;
        if (!int.TryParse(input.Trim(), out int count) || count <= 0)
        {
            WinFormsUtil.Alert("Enter a whole number greater than zero.");
            return;
        }

        SetEntry();

        int firstNew = files.Length;
        int template = files.Length > 1 ? 1 : 0;

        Array.Resize(ref files, firstNew + count);
        for (int i = firstNew; i < files.Length; i++)
            files[i] = (byte[])files[template].Clone();

        // Names and descriptions have to grow too, or the new ids are invisible to the game.
        var names = Main.Config.GetText(TextName.ItemNames);
        var flavour = Main.Config.GetText(TextName.ItemFlavor);
        int oldNames = names.Length;

        if (names.Length < files.Length)
        {
            Array.Resize(ref names, files.Length);
            for (int i = oldNames; i < names.Length; i++) names[i] = $"New Item {i}";
            Main.Config.SetText(TextName.ItemNames, names);
        }
        if (flavour.Length < files.Length)
        {
            int oldFlavour = flavour.Length;
            Array.Resize(ref flavour, files.Length);
            for (int i = oldFlavour; i < flavour.Length; i++) flavour[i] = "New item description.";
            Main.Config.SetText(TextName.ItemFlavor, flavour);
        }

        Array.Resize(ref itemflavor, files.Length);
        for (int i = 0; i < itemflavor.Length; i++) itemflavor[i] ??= "";

        CB_Item.Items.Clear();
        CB_Item.Items.AddRange(names);
        CB_Item.SelectedIndex = firstNew;

        WinFormsUtil.Alert($"Added {count} item slot(s): ids {firstNew}-{files.Length - 1}.",
            "Names and descriptions were grown to match, so the new ids are reachable. "
            + "The game's item ceiling may still need raising - see the TM Editor's expansion patches.");
    }

    private void ChangeEntry(object sender, EventArgs e)
    {
        SetEntry();
        entry = CB_Item.SelectedIndex;
        L_Index.Text = "Index: " + entry.ToString("000");

        if (entry >= files.Length)
        {
            int oldLen = files.Length;
            Array.Resize(ref files, entry + 1);
            for (int i = oldLen; i < files.Length; i++)
                files[i] = new byte[files[0].Length];
        }
        if (entry >= itemflavor.Length)
        {
            Array.Resize(ref itemflavor, entry + 1);
            for (int i = itemflavor.Length - 1; i >= 0 && itemflavor[i] == null; i--)
                itemflavor[i] = "";
        }

        RTB.Text = itemflavor[entry].Replace("\\n", Environment.NewLine);
        Grid.SelectedObject = new Item(files[entry]);
        UpdateSprite();
    }

    private void UpdateSprite()
    {
        WinFormsUtil.SetImage(PB_ItemSprite, WinFormsUtil.getIcon(entry, 0, Main.Config));
    }

    private void B_CopyTable_Click(object sender, EventArgs e)
    {
        try
        {
            if (entry < 1) return;
            
            // Just copy the current item
            var it = (Item)Grid.SelectedObject;
            string txt = string.Join(",", it.Write().Select(b => b.ToString("X2")));
            Clipboard.SetText(txt);
            WinFormsUtil.Alert("Item copied to clipboard!");
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert($"Copy failed: {ex.Message}");
        }
    }

    private void B_PasteTable_Click(object sender, EventArgs e)
    {
        string text = Clipboard.GetText();
        if (string.IsNullOrWhiteSpace(text)) return;
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        try
        {
            if (lines.Length == 1)
            {
                // Single item paste
                if (entry < 1) return;
                var bytes = lines[0].Split(',').Select(s => byte.Parse(s, System.Globalization.NumberStyles.HexNumber)).ToArray();
                files[entry] = bytes;
                Grid.SelectedObject = new Item(files[entry]);
                WinFormsUtil.Alert("Item pasted successfully!");
            }
            else
            {
                // Full table paste
                if (lines.Length != files.Length) 
                {
                    var res = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Item count mismatch.\nCopied {lines.Length}\nExpected {files.Length}\n\nPaste anyway? (Will overwrite up to {Math.Min(lines.Length, files.Length)} items)");
                    if (res != DialogResult.Yes) return;
                }

                int max = Math.Min(files.Length, lines.Length);
                for (int i = 0; i < max; i++)
                {
                    var bytes = lines[i].Split(',').Select(s => byte.Parse(s, System.Globalization.NumberStyles.HexNumber)).ToArray();
                    files[i] = bytes;
                }
                ChangeEntry(null, null);
                WinFormsUtil.Alert("Item Table pasted successfully!");
            }
        }
        catch (Exception ex)
        {
            WinFormsUtil.Alert($"Paste failed: {ex.Message}");
        }
    }

    private int entry = -1;

    private void GetEntry()
    {
        if (entry < 1) return;
        Grid.SelectedObject = new Item(files[entry]);
        RTB.Text = itemflavor[entry].Replace("\\n", Environment.NewLine);
    }

    private void SetEntry()
    {
        if (entry < 1) return;
        files[entry] = ((Item)Grid.SelectedObject).Write();
        itemflavor[entry] = RTB.Text.Replace("\r\n", "\\n").Replace("\n", "\\n");
        Main.Config.SetText(TextName.ItemFlavor, itemflavor);
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

    private void Form_Closing(object sender, FormClosingEventArgs e)
    {
        SetEntry();
    }

    private void B_Table_Click(object sender, EventArgs e)
    {
        var items = files.Select(z => new Item(z));
        Clipboard.SetText(TableUtil.GetTable(items, itemlist));
        System.Media.SystemSounds.Asterisk.Play();
    }

    private void B_MakeMega_Click(object sender, EventArgs e)
    {
        if (entry < 1) return;

        // Diancite Item ID is 764 in Gen 7 USUM
        int dianciteID = 764;
        if (dianciteID >= files.Length || files[dianciteID] == null || files[dianciteID].Length == 0)
        {
            int found = Array.IndexOf(itemlist, "Diancite");
            if (found > 0 && found < files.Length) dianciteID = found;
            else dianciteID = 659; // Fallback
        }

        if (dianciteID < files.Length && files[dianciteID] != null && files[dianciteID].Length > 0)
        {
            files[entry] = (byte[])files[dianciteID].Clone();
            Grid.SelectedObject = new Item(files[entry]);
            WinFormsUtil.Alert("Selected item converted to Mega Stone (matched Diancite data)!");
        }
    }
}
