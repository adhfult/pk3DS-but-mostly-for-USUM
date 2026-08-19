using pk3DS.Core;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class PickupEditor7 : Form
{
    public PickupEditor7(LazyGARCFile pickup)
    {
        InitializeComponent();
        g_pickup = pickup;
        // GetText returns an empty array when the item text file is missing from the loaded RomFS,
        // so blanking entry 0 unconditionally threw before the editor had drawn anything.
        var itemlist = Main.Config.GetText(TextName.ItemNames) ?? [];
        if (itemlist.Length > 0) itemlist[0] = "";
        items = itemlist.Select((v, i) => $"{v} - {i:000}").ToArray();
        SetupFLP();

        byte[] data = pickup.Files[0];
        GetList(data);
        dgvCommon.SelectionChanged += DgvCommon_SelectionChanged;
        PopulatePokemonSidebar();
    }

    private void DgvCommon_SelectionChanged(object sender, EventArgs e)
    {
        if (dgvCommon.CurrentRow == null) return;
        var cell = dgvCommon.CurrentRow.Cells[0];
        int itemId = ParseTrailingId(cell.Value?.ToString());
        if (itemId < 0) return;

        WinFormsUtil.SetImage(PB_Item, WinFormsUtil.getIcon(itemId, 0, Main.Config));
    }

    /// <summary>The id in a "Name - 123" label, or -1 when there is none.</summary>
    private static int ParseTrailingId(string label)
    {
        if (string.IsNullOrEmpty(label)) return -1;
        int dash = label.LastIndexOf('-');
        if (dash < 0) return -1;
        return int.TryParse(label[(dash + 1)..].Trim(), out int id) && id >= 0 ? id : -1;
    }

    private readonly LazyGARCFile g_pickup;
    private readonly string[] items;

    private const int Columns = 10;

    private void SetupFLP()
    {
        dgvCommon.Columns.Clear();
        // Add DataGrid
        var dgv = dgvCommon;
        {
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.AllowUserToResizeRows = false;
            dgv.AllowUserToResizeColumns = false;
            dgv.RowHeadersVisible = false;
            //dgv.ColumnHeadersVisible = false,
            dgv.MultiSelect = false;
            dgv.ShowEditingIcon = false;
            dgv.EditMode = DataGridViewEditMode.EditOnEnter;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgv.SelectionMode = DataGridViewSelectionMode.CellSelect;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.None;
        }

        int c = 0;
        var dgvItemVal = new DataGridViewComboBoxColumn
        {
            HeaderText = "Item",
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            DisplayIndex = c++,
            Width = 135,
            FlatStyle = FlatStyle.Flat,
        };
        dgv.Columns.Add(dgvItemVal);

        for (int i = 0; i < Columns; i++)
        {
            string rate = $"{(i * 10) + 1}-{(i + 1) * 10}";
            var dgvIndex = new DataGridViewTextBoxColumn();
            {
                dgvIndex.HeaderText = rate;
                dgvIndex.DisplayIndex = c++;
                dgvIndex.Width = 45;
                dgvIndex.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                dgvIndex.MaxInputLength = 2;
            }
            dgv.Columns.Add(dgvIndex);
        }

        var combo = dgv.Columns[0] as DataGridViewComboBoxColumn;
        combo.Items.AddRange(items); // add only the Item Names

        // disable sorting
        dgv.Columns.Cast<DataGridViewColumn>().ToList().ForEach(f => f.SortMode = DataGridViewColumnSortMode.NotSortable);
        dgv.CancelEdit();
    }

    private void GetList(byte[] data)
    {
        // Fill Data
        if (data == null || data.Length < 4) return;

        int rows = BitConverter.ToUInt16(data, 0) - 1; // nice editor gamefreak
        // Trust the header only as far as the file actually goes; a truncated or rewritten pickup
        // file would otherwise be read past its end.
        int fits = (data.Length - 4) / (Columns + 2);
        if (rows > fits) rows = fits;
        if (rows <= 0) return;

        dgvCommon.Rows.Add(rows);
        for (int i = 0; i < rows; i++)
        {
            int offset = 4 + (i * (Columns + 2));
            int item = BitConverter.ToUInt16(data, offset);

            var r = dgvCommon.Rows[i];
            string label = (uint)item < (uint)items.Length ? items[item] : $"??? - {item:000}";
            // The cell is a combo box, so a value absent from the column's list is rejected;
            // register the placeholder before assigning it.
            if (dgvCommon.Columns[0] is DataGridViewComboBoxColumn cb && !cb.Items.Contains(label))
                cb.Items.Add(label);
            r.Cells[0].Value = label;
            for (int j = 0; j < Columns; j++)
            {
                int rate = data[offset + 2 + j];
                r.Cells[1 + j].Value = rate.ToString();
            }
        }
    }

    private byte[] SetList()
    {
        int rows = dgvCommon.RowCount;
        int[][] rates = new int[rows][];
        for (int i = 0; i < rates.Length; i++)
            rates[i] = new int[Columns];
        for (int i = 0; i < Columns; i++)
        {
            // get column sum
            int sum = 0;
            for (int r = 0; r < rows; r++)
            {
                var cell = dgvCommon.Rows[r].Cells[i + 1];
                if (!int.TryParse(cell.Value.ToString(), out var val))
                {
                    cell.Value = 0.ToString();
                    continue;
                }
                if (val is > 100 or < 0)
                {
                    val = 0;
                    cell.Value = 0.ToString();
                }
                rates[r][i] = val;
                sum += val;
            }
            if (sum == 100) // good
                continue;

            WinFormsUtil.Alert($"Sum of Column {i + 1} needs to equal 100.", $"Got {sum}.");
            return null;
        }

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        bw.Write(rates.Length + 1); // nice editor gamefreak
        for (int i = 0; i < rows; i++)
        {
            var r = dgvCommon.Rows[i];
            string item = r.Cells[0].Value?.ToString() ?? "";
            int itemindex = Array.IndexOf(items, item);

            if (itemindex < 0)
                itemindex = ParseTrailingId(item);
            if (itemindex < 0)
            {
                WinFormsUtil.Alert($"Row {i + 1} has no recognisable item ({item}).", "Fix that row and save again.");
                return null;
            }

            bw.Write((ushort)itemindex);

            foreach (var b in rates[i])
                bw.Write((byte)b);
        }
        return ms.ToArray();
    }

    private void B_Save_Click(object sender, EventArgs e)
    {
        byte[] result = SetList();
        if (result == null)
            return;

        g_pickup[0] = result;
        g_pickup.Save();
        Close();
    }

    private void B_Cancel_Click(object sender, EventArgs e)
    {
        Close();
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, "Randomize pickup lists?"))
            return;

        // Only ids this ROM can actually name; the shared list is built for a stock item table.
        int[] validItems = Randomizer.GetRandomItemList().Where(z => (uint)z < (uint)items.Length).ToArray();
        if (validItems.Length == 0)
        {
            WinFormsUtil.Alert("No usable items to randomize with.");
            return;
        }

        int ctr = 0;
        Util.Shuffle(validItems);
        for (int r = 0; r < dgvCommon.RowCount; r++)
        {
            if (ctr >= validItems.Length)
            {
                Util.Shuffle(validItems);
                ctr = 0;
            }
            dgvCommon.Rows[r].Cells[0].Value = items[validItems[ctr++]];
        }
        WinFormsUtil.Alert("Randomized!");
    }

    private void B_AddRow_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Add a row at the bottom?"))
            return;

        int row = dgvCommon.RowCount;
        dgvCommon.Rows.Add();
        dgvCommon.Rows[row].Cells[0].Value = items[0];
        for (int i = 1; i < dgvCommon.ColumnCount; i++)
            dgvCommon.Rows[row].Cells[i].Value = 0.ToString();
    }

    private void B_DeleteRow_Click(object sender, EventArgs e)
    {
        if (DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Delete the bottom row?"))
            return;

        dgvCommon.Rows.RemoveAt(dgvCommon.RowCount - 1);
    }

    private void PopulatePokemonSidebar()
    {
        var abilities = Main.Config.GetText(TextName.AbilityNames);
        int pickupIdx = Array.IndexOf(abilities, "Pickup");
        if (pickupIdx < 0) pickupIdx = 53; // Default fallback

        var speciesNames = Main.Config.GetText(TextName.SpeciesNames);
        var personal = Main.Config.Personal;

        // Clear() detaches the old controls without disposing them, and a PictureBox never disposes
        // the Image it was given, so each rebuild used to abandon both.
        foreach (Control c in FLP_Pokemon.Controls)
        {
            if (c is PictureBox old) old.Image?.Dispose();
            c.Dispose();
        }
        FLP_Pokemon.Controls.Clear();

        int max = Math.Min(Main.Config.MaxSpeciesID, Math.Min(personal.Table.Length, speciesNames.Length));
        for (int i = 1; i < max; i++)
        {
            var p = personal[i];
            if (p.Abilities.Contains(pickupIdx))
            {
                var pb = new PictureBox
                {
                    Size = new Size(40, 30),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    Image = WinFormsUtil.GetSprite(i, 0, 0, 0, Main.Config),
                    Margin = new Padding(2)
                };
                // One shared ToolTip rather than a native tooltip window per icon.
                tips.SetToolTip(pb, speciesNames[i]);
                FLP_Pokemon.Controls.Add(pb);
            }
        }
    }

    private readonly ToolTip tips = new();

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        foreach (Control c in FLP_Pokemon.Controls)
        {
            if (c is PictureBox pb) pb.Image?.Dispose();
        }
        tips.Dispose();
        base.OnFormClosed(e);
    }
}
