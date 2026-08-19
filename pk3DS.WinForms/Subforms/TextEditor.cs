using pk3DS.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace pk3DS.WinForms;

public partial class TextEditor : Form
{
    public TextEditor(string[][] infiles, string mode)
    {
        InitializeComponent();
        WinFormsUtil.ApplyCyberSlateTheme(this, WinFormsUtil.VisualTheme.Grey);
        int currentRowIndex = -1;
        bool isSyncing = false;
        RTB_Visualizer.ReadOnly = false;
        
        // 1. Precise Shift+Enter handling (Ensuring it only runs ONCE)
        RTB_Visualizer.KeyDown += (s, e) => 
        {
            if (e.KeyCode == Keys.Enter && e.Shift)
            {
                // This completely kills the native Windows newline injection
                e.SuppressKeyPress = true; 
                e.Handled = true;
                
                // Manually insert exactly ONE physical newline
                RTB_Visualizer.SelectedText = "\n"; 
            }
        };

        // 2. Synchronize BACK to the grid (With Hardcoded Cursor Lock)
        RTB_Visualizer.TextChanged += (s, e) => 
        {
            if (isSyncing || dgv.CurrentRow == null || !RTB_Visualizer.Focused) return;
            
            isSyncing = true;
            
            // HARDCODE: Save the exact cursor position before touching the grid
            int savedCursorPos = RTB_Visualizer.SelectionStart;
            
            // Normalize any Windows (\r\n) or Mac (\r) returns into a standard \n before converting to literal tags
            string rawText = RTB_Visualizer.Text.Replace("\r\n", "\n").Replace("\r", "\n").Replace("\n", "\\n");
            
            if (dgv.CurrentRow.Cells[1].Value?.ToString() != rawText)
            {
                dgv.CurrentRow.Cells[1].Value = rawText;
            }
            
            // HARDCODE: Force the cursor back to the correct spot so typing moves forward
            RTB_Visualizer.SelectionStart = savedCursorPos;
            RTB_Visualizer.ScrollToCaret();
            
            isSyncing = false;
        };
        
        // 3. Load text ONLY on a verified row change
        dgv.SelectionChanged += (s, e) =>
        {
            if (isSyncing || dgv.CurrentRow == null) return;
            if (dgv.CurrentRow.Index == currentRowIndex) return; 
            
            currentRowIndex = dgv.CurrentRow.Index;
            
            isSyncing = true;
            string text = dgv.CurrentRow.Cells[1].Value?.ToString() ?? "";
            RTB_Visualizer.Text = text.Replace("\\n", "\n");
            isSyncing = false;
        };

        // 4. Handle direct grid edits
        dgv.CellValueChanged += (s, e) =>
        {
            if (isSyncing || dgv.CurrentRow == null) return;
            if (RTB_Visualizer.Focused) return; 
            
            isSyncing = true;
            string text = dgv.CurrentRow.Cells[1].Value?.ToString() ?? "";
            RTB_Visualizer.Text = text.Replace("\\n", "\n");
            isSyncing = false;
        };

        files = infiles;
        Mode = mode;
        
        for (int i = 0; i < files.Length; i++)
            CB_Entry.Items.Add(DescribeTextFile(i));
        CB_Entry.SelectedIndex = 0;
        dgv.EditMode = DataGridViewEditMode.EditOnEnter;
    }

    private readonly string[][] files;
    private readonly string Mode;
    private int entry = -1;

    /// <summary>
    /// Names that are stable across Gen 7 but are not part of <see cref="TextName"/>, which only
    /// covers the tables the editors need to look up by name.
    /// </summary>
    private static readonly Dictionary<int, string> ExtraGen7TextNames = new()
    {
        [013] = "Move Actions",
        [014] = "Z-Move Actions",
        [015] = "Battle Interactions",
        [016] = "Battle Effects",
        [019] = "Z-Move Names",
        [041] = "Item Names (Plural)",
        [042] = "Item Names (Plural, Alt)",
        [102] = "Ability Descriptions",
    };

    /// <summary>
    /// Label for one text file: its real name when known, otherwise a preview of what is inside it.
    /// <para>
    /// The names come from <see cref="GameConfig.GameText"/>, which is already selected per game
    /// version, rather than from indices written out by hand. The previous list hard-coded USUM
    /// indices under a "Gen 6" comment and then repeated them in version-guarded branches that
    /// could never run - the unguarded branch above always matched first - so the guards were dead
    /// and one of them (104) named the Battle Tree table as Trainer Names.
    /// </para>
    /// <para>
    /// Most of the archive has no assigned name at all. Rather than leave those as a bare number,
    /// the first line of actual text is shown, which is usually enough to recognise the file.
    /// </para>
    /// </summary>
    private string DescribeTextFile(int index)
    {
        string label = index.ToString("000");
        if (Mode != "gametext")
            return label;

        var reference = Main.Config?.GameText;
        if (reference != null)
        {
            foreach (var r in reference)
            {
                if (r.Index == index)
                    return $"{label} - {Prettify(r.Name.ToString())}";
            }
        }

        if (ExtraGen7TextNames.TryGetValue(index, out string known))
            return $"{label} - {known}";

        string preview = FirstLineOf(index);
        return preview.Length == 0 ? label : $"{label} - “{preview}”";
    }

    /// <summary>"SpeciesClassifications" -> "Species Classifications".</summary>
    private static string Prettify(string name) =>
        System.Text.RegularExpressions.Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ");

    private string FirstLineOf(int index)
    {
        if (files == null || index < 0 || index >= files.Length) return "";
        var lines = files[index];
        if (lines == null) return "";

        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            // Strip the in-game control codes so the preview is readable.
            string clean = line.Replace("\\n", " ").Replace("\\r", " ").Replace("\\c", " ").Trim();
            if (clean.Length == 0) continue;
            return clean.Length > 40 ? clean[..40].TrimEnd() + "..." : clean;
        }
        return "";
    }

    private void B_Export_Click(object sender, EventArgs e)
    {
        if (files.Length == 0) return;
        var Dump = new SaveFileDialog { Filter = "Text File|*.txt" };
        if (Dump.ShowDialog() != DialogResult.OK) return;
        bool newline = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Remove newline formatting codes? (\\n,\\r,\\c)", "Removing newline formatting will make it more readable but will prevent any importing of that dump.") == DialogResult.Yes;
        ExportTextFile(Dump.FileName, newline, files);
    }

    private void B_Import_Click(object sender, EventArgs e)
    {
        if (files.Length == 0) return;
        var Dump = new OpenFileDialog { Filter = "Text File|*.txt" };
        if (Dump.ShowDialog() != DialogResult.OK) return;
        
        if (!ImportTextFile(Dump.FileName)) return;

        ChangeEntry(null, null);
        WinFormsUtil.Alert("Imported Text from Input Path:", Dump.FileName);
    }

    public static void ExportTextFile(string fileName, bool newline, string[][] fileData)
    {
        using var ms = new MemoryStream();
        ms.Write([0xFF, 0xFE], 0, 2); // Write Unicode BOM
        using (var tw = new StreamWriter(ms, new UnicodeEncoding()))
        {
            for (int i = 0; i < fileData.Length; i++)
            {
                string[] data = fileData[i];
                tw.WriteLine("~~~~~~~~~~~~~~~");
                tw.WriteLine("Text File : " + i);
                tw.WriteLine("~~~~~~~~~~~~~~~");
                if (data == null) continue;
                foreach (string line in data)
                {
                    tw.WriteLine(newline
                        ? line.Replace("\\n\\n", " ").Replace("\\n", " ").Replace("\\c", "").Replace("\\r", "").Replace("\\\\", "\\").Replace("\\[", "[")
                        : line);
                }
            }
        }
        File.WriteAllBytes(fileName, ms.ToArray());
    }

    private bool ImportTextFile(string fileName)
    {
        string[] fileText = File.ReadAllLines(fileName, Encoding.Unicode);
        string[][] textLines = new string[files.Length][];
        int ctr = 0;
        bool newlineFormatting = false;
        
        for (int i = 0; i < fileText.Length; i++)
        {
            string line = fileText[i];
            if (line != "~~~~~~~~~~~~~~~") continue;
            string[] brokenLine = fileText[i++ + 1].Split([" : "], StringSplitOptions.None);
            if (brokenLine.Length != 2 || Util.ToInt32(brokenLine[1]) != ctr)
            { WinFormsUtil.Error($"Invalid Line @ {i}, expected Text File : {ctr}"); return false; }
            i += 2; 
            List<string> Lines = [];
            while (i < fileText.Length && fileText[i] != "~~~~~~~~~~~~~~~")
            {
                Lines.Add(fileText[i]);
                newlineFormatting |= fileText[i].Contains("\\n"); 
                i++;
            }
            i--;
            textLines[ctr++] = [.. Lines];
        }

        if (ctr != files.Length)
        {
            WinFormsUtil.Error("The amount of Text Files in the input file does not match.", $"Received: {ctr}, Expected: {files.Length}"); return false;
        }
        if (!newlineFormatting)
        {
            WinFormsUtil.Error("The input Text Files do not have the ingame newline formatting codes (\\n,\\r,\\c).", "When exporting text, do not remove newline formatting."); return false;
        }

        for (int i = 0; i < files.Length; i++)
        {
            try { files[i] = textLines[i]; }
            catch (Exception e) { WinFormsUtil.Error($"The input Text File (# {i}) failed to convert:", e.ToString()); return false; }
        }

        return true;
    }

    private void ChangeEntry(object sender, EventArgs e)
    {
        if (entry > -1 && sender != null)
        {
            try { files[entry] = GetCurrentDGLines(); }
            catch (Exception ex) { WinFormsUtil.Error(ex.ToString()); }
        }

        entry = CB_Entry.SelectedIndex;
        SetStringsDataGridView(files[entry]);
    }

    private void SetStringsDataGridView(string[] textArray)
    {
        dgv.Rows.Clear();
        dgv.Columns.Clear();
        if (textArray == null || textArray.Length == 0) return;
        
        dgv.AllowUserToResizeColumns = false;
        DataGridViewColumn dgvLine = new DataGridViewTextBoxColumn
        {
            HeaderText = "Line", DisplayIndex = 0, Width = 32, ReadOnly = true, SortMode = DataGridViewColumnSortMode.NotSortable,
        };
        dgvLine.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;

        var dgvText = new DataGridViewTextBoxColumn
        {
            HeaderText = "Text", DisplayIndex = 1, SortMode = DataGridViewColumnSortMode.NotSortable, AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
        };

        dgv.Columns.Add(dgvLine);
        dgv.Columns.Add(dgvText);
        dgv.Rows.Add(textArray.Length);

        for (int i = 0; i < textArray.Length; i++)
        {
            dgv.Rows[i].Cells[0].Value = i;
            dgv.Rows[i].Cells[1].Value = textArray[i];
        }
    }

    private string[] GetCurrentDGLines()
    {
        string[] lines = new string[dgv.RowCount];
        for (int i = 0; i < dgv.RowCount; i++)
            lines[i] = (string)dgv.Rows[i].Cells[1].Value;
        return lines;
    }
    


    private int GetNumberPrompt(string title, string promptText, int defaultValue)
    {
        using Form promptForm = new Form()
        {
            Width = 320, Height = 170, FormBorderStyle = FormBorderStyle.FixedDialog, 
            Text = title, StartPosition = FormStartPosition.CenterParent, 
            MaximizeBox = false, MinimizeBox = false
        };
        Label textLabel = new Label() { Left = 20, Top = 20, Text = promptText, AutoSize = true };
        NumericUpDown nud = new NumericUpDown() { Left = 20, Top = 50, Width = 260, Minimum = 1, Maximum = 1000, Value = defaultValue };
        Button confirmation = new Button() { Text = "OK", Left = 110, Width = 100, Top = 90, DialogResult = DialogResult.OK };
        
        confirmation.Click += (s, e) => { promptForm.DialogResult = DialogResult.OK; promptForm.Close(); };
        
        promptForm.Controls.Add(nud);
        promptForm.Controls.Add(confirmation);
        promptForm.Controls.Add(textLabel);
        promptForm.AcceptButton = confirmation;

        return promptForm.ShowDialog() == DialogResult.OK ? (int)nud.Value : 0;
    }

    private void B_AddLine_Click(object sender, EventArgs e)
    {
        int count = GetNumberPrompt("Add Lines", "How many lines do you want to add after the selection?", 1);
        if (count <= 0) return;

        int currentRow = 0;
        bool appendToEnd = false;
        try { currentRow = dgv.CurrentRow.Index; } catch { appendToEnd = true; } 

        if (appendToEnd)
        {
            dgv.Rows.Add(count);
        }
        else
        {
            if (dgv.Rows.Count != 1 && (currentRow < dgv.Rows.Count - 1 || currentRow == 0))
                if (ModifierKeys != Keys.Control && currentRow != 0 && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Inserting {count} row(s) in between lines will shift all subsequent lines.", "Continue?") != DialogResult.Yes) return;
            
            for (int i = 0; i < count; i++) dgv.Rows.Insert(currentRow + 1 + i);
        }

        for (int i = 0; i < dgv.Rows.Count; i++) dgv.Rows[i].Cells[0].Value = i.ToString();
    }

    private void B_AddLineBefore_Click(object sender, EventArgs e)
    {
        int count = GetNumberPrompt("Add Lines", "How many lines do you want to add before the selection?", 1);
        if (count <= 0) return;

        int currentRow = 0;
        bool appendToEnd = false;
        try { currentRow = dgv.CurrentRow.Index; } catch { appendToEnd = true; }

        if (appendToEnd)
        {
            dgv.Rows.Add(count);
        }
        else
        {
            if (dgv.Rows.Count != 1)
                if (ModifierKeys != Keys.Control && WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Inserting {count} row(s) before lines will shift all subsequent lines.", "Continue?") != DialogResult.Yes) return;
            
            for (int i = 0; i < count; i++) dgv.Rows.Insert(currentRow);
        }

        for (int i = 0; i < dgv.Rows.Count; i++) dgv.Rows[i].Cells[0].Value = i.ToString();
    }

    private void B_RemoveLine_Click(object sender, EventArgs e)
    {
        if (dgv.CurrentRow == null) return;
        int currentRow = dgv.CurrentRow.Index;
        int count = GetNumberPrompt("Remove Lines", "How many lines do you want to remove?", 1);
        if (count <= 0) return;

        if (currentRow + count > dgv.Rows.Count) count = dgv.Rows.Count - currentRow; 
        if (currentRow < dgv.Rows.Count - count)
            if (ModifierKeys != Keys.Control && DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Deleting {count} row(s) above other lines will shift all subsequent lines.", "Continue?")) return;

        for (int i = 0; i < count; i++) dgv.Rows.RemoveAt(currentRow);
        for (int i = 0; i < dgv.Rows.Count; i++) dgv.Rows[i].Cells[0].Value = i.ToString();
    }

    private void B_Search_Click(object sender, EventArgs e)
    {
        string query = TB_Search.Text;
        if (string.IsNullOrEmpty(query)) return;

        int startRow = dgv.CurrentCell?.RowIndex ?? 0;
        int startCol = dgv.CurrentCell?.ColumnIndex ?? 0;

        for (int i = startRow; i < dgv.RowCount; i++)
        {
            for (int j = (i == startRow ? startCol + 1 : 0); j < dgv.ColumnCount; j++)
            {
                if (dgv[j, i].Value?.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                {
                    dgv.CurrentCell = dgv[j, i];
                    return;
                }
            }
        }
        WinFormsUtil.Alert("No more occurrences found.");
    }

    private void B_SearchPrev_Click(object sender, EventArgs e)
    {
        string query = TB_Search.Text;
        if (string.IsNullOrEmpty(query)) return;

        int startRow = dgv.CurrentCell?.RowIndex ?? dgv.RowCount - 1;
        int startCol = dgv.CurrentCell?.ColumnIndex ?? dgv.ColumnCount - 1;

        for (int i = startRow; i >= 0; i--)
        {
            for (int j = (i == startRow ? startCol - 1 : dgv.ColumnCount - 1); j >= 0; j--)
            {
                if (dgv[j, i].Value?.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) == true)
                {
                    dgv.CurrentCell = dgv[j, i];
                    return;
                }
            }
        }
        WinFormsUtil.Alert("No previous occurrences found.");
    }

    private void B_BatchReplace_Click(object sender, EventArgs e)
    {
        string query = TB_Search.Text;
        if (string.IsNullOrEmpty(query)) return;
        string replace = WinFormsUtil.PromptInput("Enter replacement text:", "Batch Replace");
        if (replace == null) return;

        int count = 0;
        for (int i = 0; i < dgv.RowCount; i++)
        {
            var cell = dgv[1, i]; // col 1 is text
            string text = cell.Value?.ToString() ?? "";
            if (text.Contains(query, StringComparison.OrdinalIgnoreCase))
            {
                // Note: string.Replace(..., StringComparison) returns a new string
                cell.Value = System.Text.RegularExpressions.Regex.Replace(text, System.Text.RegularExpressions.Regex.Escape(query), replace, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                count++;
            }
        }
        WinFormsUtil.Alert($"Replaced {count} occurrences.");
    }

    private void TextEditor_FormClosing(object sender, FormClosingEventArgs e)
    {
        dgv.EndEdit();
        if (entry > -1) files[entry] = GetCurrentDGLines();
    }

    private void B_Randomize_Click(object sender, EventArgs e)
    {
        if (Mode == "gametext" && DialogResult.Yes != WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Randomizing Game Text is dangerous!", "Continue?")) return;

        var dr = WinFormsUtil.Prompt(MessageBoxButtons.YesNoCancel, $"Yes: Randomize ALL{Environment.NewLine}No: Randomize current textfile{Environment.NewLine}Cancel: Abort");
        if (dr == DialogResult.Cancel) return;

        var drs = WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Smart shuffle:{Environment.NewLine}Yes: Shuffle if no Variable present{Environment.NewLine}No: Pure random!");
        if (drs == DialogResult.Cancel) return;

        bool all = dr == DialogResult.Yes;
        bool smart = drs == DialogResult.Yes;

        if (entry > -1) files[entry] = GetCurrentDGLines();

        int start = all ? 0 : entry;
        int end = all ? files.Length - 1 : entry;

        List<string> strings = [];
        for (int i = start; i <= end; i++)
        {
            string[] data = files[i];
            strings.AddRange(smart ? data.Where(line => !line.Contains('[')) : data);
        }

        string[] pool = [.. strings];
        Util.Shuffle(pool);

        int ctr = 0;
        for (int i = start; i <= end; i++)
        {
            string[] data = files[i];
            for (int j = 0; j < data.Length; j++) 
            {
                if (!smart || !data[j].Contains('[')) data[j] = pool[ctr++];
            }
            files[i] = data;
        }

        SetStringsDataGridView(files[entry]);
        WinFormsUtil.Alert("Strings randomized!");
    }

    private string StripControlCodes(string input)
    {
        if (string.IsNullOrEmpty(input)) return "";
        
        // 1. Remove standard control codes, EXCEPT for \n which we process into linebreaks
        // Replace \r with space if needed, strip others.
        string result = input.Replace("\\n", Environment.NewLine)
                             .Replace("\\r", "")
                             .Replace("\\c", "")
                             .Replace("\\f", "");
        
        // 2. Remove [VAR] type codes using a simple loop
        StringBuilder sb = new();
        bool inBrackets = false;
        for (int i = 0; i < result.Length; i++)
        {
            char c = result[i];
            if (c == '[') { inBrackets = true; continue; }
            if (c == ']') { inBrackets = false; continue; }
            if (!inBrackets) sb.Append(c);
        }
        
        result = sb.ToString();
        // Final cleanup of formatting codes (reverse of what's expected for visualizer)
        result = result.Replace("\\n", Environment.NewLine)
                       .Replace("\\r", "")
                       .Replace("\\c", "")
                       .Replace("\\f", "")
                       .Replace("\\\\", "\\");

        return result.Trim();
    }
}