using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.Modding.Research;

namespace pk3DS.WinForms;

/// <summary>
/// Edits the level cap progression: which story flag raises the cap, and to what.
/// </summary>
public sealed class LevelCapEditor : Form
{
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
    };

    private readonly TextBox _problems = new()
    {
        Multiline = true, Dock = DockStyle.Bottom, Height = 90, ReadOnly = true,
        ScrollBars = ScrollBars.Vertical, Font = new Font(FontFamily.GenericMonospace, 8.25f),
    };

    /// <summary>The edited table, or null if the dialog was cancelled.</summary>
    public LevelCapTable Result { get; private set; }

    public LevelCapEditor(LevelCapTable start)
    {
        Text = "Level Cap";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(620, 460);
        Size = new Size(720, 560);

        // Picking from the catalogue fills the offset and bit in, so the common case never needs
        // hex. The columns stay editable for a flag the workbook does not list.
        var flagCol = new DataGridViewComboBoxColumn
        {
            HeaderText = "Where in game",
            Name = "Label",
            FillWeight = 190,
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
            FlatStyle = FlatStyle.Flat,
            AutoComplete = true,
        };
        foreach (var f in LevelCapTable.KnownFlags) flagCol.Items.Add(f.Label);
        _grid.Columns.Add(flagCol);

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Flag offset", Name = "Offset", FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Flag bit", Name = "Bit", FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Level cap", Name = "Cap", FillWeight = 60 });

        // A label absent from the catalogue would be rejected by the combo, so any custom one that
        // arrives with the incoming table is registered before it is shown.
        foreach (var e in (start ?? LevelCapTable.Default()).Entries)
            if (!flagCol.Items.Contains(e.Label)) flagCol.Items.Add(e.Label);

        _grid.CellValueChanged += (_, ev) =>
        {
            if (ev.RowIndex < 0 || _grid.Columns[ev.ColumnIndex].Name != "Label") return;
            string picked = _grid.Rows[ev.RowIndex].Cells["Label"].Value?.ToString() ?? "";
            var f = LevelCapTable.KnownFlags.FirstOrDefault(k => k.Label == picked);
            if (f == null) return;
            _grid.Rows[ev.RowIndex].Cells["Offset"].Value = $"0x{f.Offset:X2}";
            _grid.Rows[ev.RowIndex].Cells["Bit"].Value = $"0x{f.Bit:X2}";
            Revalidate();
        };
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };

        foreach (var e in (start ?? LevelCapTable.Default()).Entries.OrderBy(e => e.Cap))
            _grid.Rows.Add(e.Label, $"0x{e.FlagOffset:X2}", $"0x{e.FlagBit:X2}", e.Cap.ToString());

        var bOk = new Button { Text = "Use this", Width = 100 };
        var bCancel = new Button { Text = "Cancel", Width = 80 };
        var bReset = new Button { Text = "Reset to default", Width = 130 };
        var bAdd = new Button { Text = "Add row", Width = 90 };
        var bDel = new Button { Text = "Delete row", Width = 100 };

        bOk.Click += (_, _) =>
        {
            var t = Read(out string error);
            if (t == null) { _problems.Text = error; return; }
            Result = t;
            DialogResult = DialogResult.OK;
            Close();
        };
        bCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        bReset.Click += (_, _) =>
        {
            _grid.Rows.Clear();
            foreach (var e in LevelCapTable.Default().Entries.OrderBy(e => e.Cap))
                _grid.Rows.Add(e.Label, $"0x{e.FlagOffset:X2}", $"0x{e.FlagBit:X2}", e.Cap.ToString());
            Revalidate();
        };
        bAdd.Click += (_, _) =>
        {
            // Seed from the first catalogue flag not already used, so a new row starts valid.
            var used = _grid.Rows.Cast<DataGridViewRow>().Where(x => !x.IsNewRow)
                .Select(x => x.Cells["Label"].Value?.ToString() ?? "").ToHashSet();
            var next = LevelCapTable.KnownFlags.FirstOrDefault(f => !used.Contains(f.Label))
                       ?? LevelCapTable.KnownFlags[0];
            _grid.Rows.Add(next.Label, $"0x{next.Offset:X2}", $"0x{next.Bit:X2}", "100");
            Revalidate();
        };
        bDel.Click += (_, _) =>
        {
            if (_grid.CurrentRow != null && !_grid.CurrentRow.IsNewRow) _grid.Rows.Remove(_grid.CurrentRow);
            Revalidate();
        };

        _grid.CellEndEdit += (_, _) => Revalidate();

        var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 34 };
        bar.Controls.AddRange([bOk, bCancel, bReset, bAdd, bDel]);

        var help = new Label
        {
            Dock = DockStyle.Top, Height = 34, ForeColor = Color.Gray, Padding = new Padding(6, 4, 0, 0),
            Text = "Each row is a story flag and the cap that applies once it is set. The highest satisfied "
                 + "row wins.\nFlags are one bit; offsets and bits are hex.",
        };

        Controls.Add(_grid);
        Controls.Add(_problems);
        Controls.Add(bar);
        Controls.Add(help);
        Revalidate();
    }

    private static bool TryByte(string text, out byte value)
    {
        value = 0;
        string s = (text ?? "").Trim();
        if (s.Length == 0) return false;
        bool hex = s.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (hex) s = s[2..];
        try
        {
            int v = Convert.ToInt32(s, hex ? 16 : 10);
            if (v is < 0 or > 255) return false;
            value = (byte)v;
            return true;
        }
        catch { return false; }
    }

    private LevelCapTable Read(out string error)
    {
        error = null;
        var entries = new List<LevelCapEntry>();

        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (row.IsNewRow) continue;
            string label = row.Cells["Label"].Value?.ToString()?.Trim() ?? "";
            if (label.Length == 0) continue;

            if (!TryByte(row.Cells["Offset"].Value?.ToString(), out byte off))
            { error = $"'{label}': flag offset is not a byte (use hex like 0x11)."; return null; }
            if (!TryByte(row.Cells["Bit"].Value?.ToString(), out byte bit))
            { error = $"'{label}': flag bit is not a byte (use hex like 0x01)."; return null; }
            if (!TryByte(row.Cells["Cap"].Value?.ToString(), out byte cap))
            { error = $"'{label}': level cap is not a number 1-255."; return null; }

            entries.Add(new LevelCapEntry(label, off, bit, cap));
        }

        if (entries.Count == 0) { error = "No rows; add at least one."; return null; }
        return new LevelCapTable { Entries = entries };
    }

    private void Revalidate()
    {
        var t = Read(out string error);
        if (t == null) { _problems.Text = error; return; }

        var problems = t.Validate();
        _problems.Text = problems.Count == 0
            ? $"OK - {t.Entries.Count} step(s), {t.ToBytes().Length} bytes."
            : string.Join(Environment.NewLine, problems.Select(p => "- " + p));
    }
}
