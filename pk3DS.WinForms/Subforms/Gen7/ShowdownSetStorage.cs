using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core;

namespace pk3DS.WinForms
{
    public partial class ShowdownSetStorage : Form
    {
        private List<int> _filteredIndices = new List<int>();
        public string SelectedSet { get; private set; }

        public ShowdownSetStorage()
        {
            InitializeComponent();
            WinFormsUtil.ApplyCyberSlateTheme(this, WinFormsUtil.VisualTheme.Grey);
            RefreshList();
        }

        private void RefreshList(string filter = "")
        {
            LB_Sets.BeginUpdate();
            LB_Sets.Items.Clear();
            _filteredIndices.Clear();

            string[] allDisplay = ShowdownSetManager.GetSetListStrings();
            string query = filter.Trim();

            for (int i = 0; i < ShowdownSetManager.Sets.Count; i++)
            {
                var s = ShowdownSetManager.Sets[i];
                if (string.IsNullOrEmpty(query) ||
                    (allDisplay.Length > i && allDisplay[i].Contains(query, StringComparison.OrdinalIgnoreCase)) ||
                    s.Content.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    s.Nickname.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    _filteredIndices.Add(i);
                    LB_Sets.Items.Add(allDisplay.Length > i ? allDisplay[i] : $"Set [{i + 1}]");
                }
            }

            LB_Sets.EndUpdate();

            UpdateCapacityLabel();

            if (LB_Sets.Items.Count > 0)
            {
                LB_Sets.SelectedIndex = 0;
            }
            else
            {
                RTB_Preview.Clear();
            }
        }

        private void UpdateCapacityLabel()
        {
            int total = ShowdownSetManager.Sets.Count;
            int max = ShowdownSetManager.MaxCapacity;
            if (string.IsNullOrEmpty(TB_Search.Text.Trim()))
            {
                L_Count.Text = $"Capacity: {total} / {max} Sets";
            }
            else
            {
                L_Count.Text = $"Showing: {LB_Sets.Items.Count} (Total: {total} / {max})";
            }
            L_Count.ForeColor = total >= max ? Color.IndianRed : Color.Gainsboro;
        }

        private int GetCurrentRealIndex()
        {
            int sel = LB_Sets.SelectedIndex;
            if (sel >= 0 && sel < _filteredIndices.Count)
                return _filteredIndices[sel];
            return -1;
        }

        private void LB_Sets_SelectedIndexChanged(object sender, EventArgs e)
        {
            int realIdx = GetCurrentRealIndex();
            if (realIdx < 0 || realIdx >= ShowdownSetManager.Sets.Count)
            {
                RTB_Preview.Clear();
                L_PreviewHeader.Text = "Set Preview:";
                return;
            }

            L_PreviewHeader.Text = $"Set Preview [{realIdx + 1} / {ShowdownSetManager.Sets.Count}]:";
            RTB_Preview.Text = ShowdownSetManager.GetSetText(realIdx);
        }

        private void TB_Search_TextChanged(object sender, EventArgs e)
        {
            RefreshList(TB_Search.Text);
        }

        private void B_Add_Click(object sender, EventArgs e)
        {
            string text = Clipboard.GetText().Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                WinFormsUtil.Alert("Clipboard is empty! Copy Pokémon Showdown sets to clipboard first.");
                return;
            }

            if (ShowdownSetManager.Sets.Count >= ShowdownSetManager.MaxCapacity)
            {
                WinFormsUtil.Alert($"Storage capacity limit ({ShowdownSetManager.MaxCapacity} sets) has been reached.");
                return;
            }

            var parts = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            int added = 0;
            foreach (var part in parts)
            {
                if (ShowdownSetManager.Sets.Count >= ShowdownSetManager.MaxCapacity)
                    break;

                string p = part.Trim();
                if (string.IsNullOrWhiteSpace(p)) continue;
                string name = ShowdownSetManager.GetNickname(p);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = WinFormsUtil.PromptInput($"Enter a nickname for this Pokémon:\n{p.Split('\n')[0]}", "Add Showdown Set");
                    if (name == null) break;
                }

                if (ShowdownSetManager.AddSet(p, name))
                    added++;
            }

            if (added > 0)
            {
                RefreshList(TB_Search.Text);
                WinFormsUtil.Alert($"Successfully added {added} Showdown set(s) to storage!");
            }
            else if (ShowdownSetManager.Sets.Count >= ShowdownSetManager.MaxCapacity)
            {
                WinFormsUtil.Alert($"Storage capacity limit ({ShowdownSetManager.MaxCapacity} sets) reached.");
            }
        }

        private void B_ImportFile_Click(object sender, EventArgs e)
        {
            if (ShowdownSetManager.Sets.Count >= ShowdownSetManager.MaxCapacity)
            {
                WinFormsUtil.Alert($"Storage is full ({ShowdownSetManager.MaxCapacity} sets max).");
                return;
            }

            using var ofd = new OpenFileDialog
            {
                Title = "Import Showdown Sets File",
                Filter = "Text Files (*.txt)|*.txt|All Files (*.*)|*.*"
            };

            if (ofd.ShowDialog() != DialogResult.OK) return;

            string text = File.ReadAllText(ofd.FileName).Trim();
            if (string.IsNullOrWhiteSpace(text)) return;

            var parts = text.Split(new[] { "\r\n\r\n", "\n\n" }, StringSplitOptions.RemoveEmptyEntries);
            int added = 0;
            foreach (var part in parts)
            {
                if (ShowdownSetManager.Sets.Count >= ShowdownSetManager.MaxCapacity) break;
                string p = part.Trim();
                if (string.IsNullOrWhiteSpace(p)) continue;
                string name = ShowdownSetManager.GetNickname(p);
                if (ShowdownSetManager.AddSet(p, name))
                    added++;
            }

            if (added > 0)
            {
                RefreshList(TB_Search.Text);
                WinFormsUtil.Alert($"Imported {added} set(s) from {Path.GetFileName(ofd.FileName)}!");
            }
        }

        private void B_ExportFile_Click(object sender, EventArgs e)
        {
            if (ShowdownSetManager.Sets.Count == 0)
            {
                WinFormsUtil.Alert("No sets stored to export.");
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Title = "Export All Showdown Sets",
                Filter = "Text Files (*.txt)|*.txt",
                FileName = "Stored_Showdown_Sets.txt"
            };

            if (sfd.ShowDialog() != DialogResult.OK) return;

            var allContent = string.Join("\r\n\r\n", ShowdownSetManager.Sets.Select(s => s.Content.Trim()));
            File.WriteAllText(sfd.FileName, allContent);
            WinFormsUtil.Alert($"Exported {ShowdownSetManager.Sets.Count} sets to {Path.GetFileName(sfd.FileName)}!");
        }

        private void B_Delete_Click(object sender, EventArgs e)
        {
            int realIdx = GetCurrentRealIndex();
            if (realIdx < 0) return;
            if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, "Delete this set from storage?") != DialogResult.Yes) return;

            ShowdownSetManager.RemoveSet(realIdx);
            RefreshList(TB_Search.Text);
        }

        private void B_ClearAll_Click(object sender, EventArgs e)
        {
            if (ShowdownSetManager.Sets.Count == 0) return;
            if (WinFormsUtil.Prompt(MessageBoxButtons.YesNo, $"Are you sure you want to delete all {ShowdownSetManager.Sets.Count} stored sets?") != DialogResult.Yes) return;
            ShowdownSetManager.ClearAll();
            RefreshList();
        }

        private void B_Copy_Click(object sender, EventArgs e)
        {
            int realIdx = GetCurrentRealIndex();
            if (realIdx < 0) return;
            Clipboard.SetText(ShowdownSetManager.GetSetText(realIdx));
            WinFormsUtil.Alert("Showdown set copied to clipboard!");
        }

        private void B_Use_Click(object sender, EventArgs e)
        {
            int realIdx = GetCurrentRealIndex();
            if (realIdx < 0) return;
            SelectedSet = ShowdownSetManager.GetSetText(realIdx);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void B_Close_Click(object sender, EventArgs e) => Close();
    }
}
