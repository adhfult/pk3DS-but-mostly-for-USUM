using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pk3DS.Core.Modding.Research;

namespace pk3DS.WinForms;

/// <summary>
/// Picks a starting shape for a custom function and fills in the parts that vary.
/// </summary>
public sealed class FunctionTemplatePicker : Form
{
    private readonly ListBox LB_Templates;
    private readonly TextBox TB_Detail;
    private readonly TextBox TB_Preview;
    private readonly TableLayoutPanel PNL_Params;
    private readonly TextBox TB_Name;
    private readonly Dictionary<string, TextBox> paramBoxes = [];

    /// <summary>Symbols for the loaded ROM, so the preview can show real addresses. May be null.</summary>
    private readonly ArmSymbolTable symbols;

    /// <summary>The resolved ROM, so the timing can be read from the element the shape copies.</summary>
    private readonly BattleMechanicMap map;

    private readonly ComboBox CB_Timing;

    /// <summary>The definition built from the chosen template, or null if cancelled.</summary>
    public CustomFunctionDefinition Result { get; private set; }

    public FunctionTemplatePicker(ArmSymbolTable symbols = null, BattleMechanicMap map = null)
    {
        this.symbols = symbols;
        this.map = map;

        Text = "New Function from Template";
        ClientSize = new Size(1100, 600);
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = MaximizeBox = false;

        LB_Templates = new ListBox { Location = new Point(12, 12), Size = new Size(296, 500) };
        foreach (var t in FunctionTemplates.All) LB_Templates.Items.Add(t);
        LB_Templates.DisplayMember = nameof(FunctionTemplate.Name);
        LB_Templates.SelectedIndexChanged += (_, _) => ShowTemplate();

        TB_Detail = new TextBox
        {
            Location = new Point(316, 12),
            Size = new Size(340, 206),
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font(FontFamily.GenericMonospace, 8.5f),
        };

        var lTiming = new Label { Text = "Timing:", Location = new Point(316, 229), AutoSize = true };
        CB_Timing = new ComboBox
        {
            Location = new Point(372, 226),
            Size = new Size(100, 22),
            DropDownStyle = ComboBoxStyle.DropDown,
        };

        PNL_Params = new TableLayoutPanel
        {
            Location = new Point(316, 258),
            Size = new Size(340, 254),
            ColumnCount = 2,
            AutoScroll = true,
        };

        // The preview is the whole point of the redesign: you can see the finished routine before
        // committing to it, which is the fastest way to tell that nothing is left to fill in.
        var lPreview = new Label { Text = "Generated code:", Location = new Point(668, 12), AutoSize = true };
        TB_Preview = new TextBox
        {
            Location = new Point(668, 32),
            Size = new Size(420, 480),
            Multiline = true,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = ScrollBars.Both,
            Font = new Font(FontFamily.GenericMonospace, 8.5f),
        };

        var lName = new Label { Text = "Function name:", Location = new Point(12, 528), AutoSize = true };
        TB_Name = new TextBox { Location = new Point(110, 525), Size = new Size(240, 22) };

        var bCreate = new Button { Text = "Create", Location = new Point(898, 525), Size = new Size(90, 30) };
        var bCancel = new Button { Text = "Cancel", Location = new Point(996, 525), Size = new Size(90, 30), DialogResult = DialogResult.Cancel };

        bCreate.Click += (_, _) =>
        {
            if (LB_Templates.SelectedItem is not FunctionTemplate t)
            {
                WinFormsUtil.Alert("Pick a template first.");
                return;
            }

            Result = t.Build(TB_Name.Text.Trim(), CurrentValues());
            if (TryReadTiming(out byte timing)) Result.Timing = timing;
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange([LB_Templates, TB_Detail, lTiming, CB_Timing, PNL_Params,
                           lPreview, TB_Preview, lName, TB_Name, bCreate, bCancel]);
        AcceptButton = bCreate;
        CancelButton = bCancel;

        if (LB_Templates.Items.Count > 0) LB_Templates.SelectedIndex = 0;
    }

    private Dictionary<string, string> CurrentValues() =>
        paramBoxes.ToDictionary(kv => kv.Key, kv => kv.Value.Text);

    private void ShowTemplate()
    {
        if (LB_Templates.SelectedItem is not FunctionTemplate t) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(t.Summary);
        sb.AppendLine();
        sb.AppendLine($"Attaches to : {t.Mechanic()}");
        sb.AppendLine($"Target      : {t.Target}");
        sb.AppendLine($"Reference   : {t.CorpusReference}");

        var suggestion = TemplateTiming.Suggest(t, map);
        FillTimingBox(suggestion);

        sb.AppendLine();
        if (suggestion.Certain)
        {
            sb.AppendLine("Ready to use. The timing below was read from your ROM and");
            sb.AppendLine("addresses resolve at install time, so the only thing left");
            sb.AppendLine("to choose is which move/ability/item it attaches to.");
        }
        else
        {
            sb.AppendLine("Ready to use. Addresses resolve to your ROM at install time,");
            sb.AppendLine("so there is nothing to look up. You choose two things:");
            sb.AppendLine("  1. the move/ability/item it attaches to");
            sb.AppendLine("  2. the timing - candidates are listed in the box below");
        }

        sb.AppendLine();
        sb.AppendLine("Timing: " + suggestion.Source);
        if (!string.IsNullOrEmpty(t.TimingHint))
            sb.AppendLine("        " + t.TimingHint);

        if (t.Verify.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Worth knowing:");
            foreach (string v in t.Verify) sb.AppendLine("  - " + v);
        }
        TB_Detail.Text = sb.ToString();

        if (string.IsNullOrWhiteSpace(TB_Name.Text))
            TB_Name.Text = t.Name.Replace("Ability: ", "").Replace("Item: ", "").Replace("Move: ", "")
                            .Replace(" ", "");

        // Rebuild the parameter rows for this template.
        PNL_Params.SuspendLayout();
        foreach (Control c in PNL_Params.Controls) c.Dispose();
        PNL_Params.Controls.Clear();
        paramBoxes.Clear();

        foreach (var p in t.Parameters)
        {
            var lbl = new Label { Text = p.Label, AutoSize = true, Padding = new Padding(0, 5, 6, 0) };

            var choices = GameIds.For(p.Key);
            Control input;
            TextBox box;

            if (choices is { Count: > 0 })
            {
                var combo = new ComboBox { Width = 220, DropDownStyle = ComboBoxStyle.DropDown };
                foreach (var c in choices) combo.Items.Add(c.ToString());

                // Show the default as its named entry when it matches one.
                string shown = p.Default;
                if (int.TryParse(p.Default, out int dv))
                    foreach (var c in choices)
                        if (c.Value == dv) { shown = c.ToString(); break; }
                combo.Text = shown;

                // The engine's own value is what goes into the body, not the label beside it.
                box = new TextBox { Text = p.Default, Visible = false };
                combo.TextChanged += (_, _) =>
                {
                    string s = combo.Text.Trim();
                    int dash = s.IndexOf(" - ", StringComparison.Ordinal);
                    box.Text = dash > 0 ? s[..dash] : s;
                    UpdatePreview();
                };
                input = combo;
            }
            else
            {
                box = new TextBox { Text = p.Default, Width = 190 };
                box.TextChanged += (_, _) => UpdatePreview();
                input = box;
            }

            string help = p.Help;
            if (choices is { Count: > 0 } && choices.Any(c => !c.Confirmed))
                help += (help.Length > 0 ? "\n\n" : "")
                      + "Entries marked (unconfirmed) come from the research notes rather than from "
                      + "something this program can check against the ROM. Dry run before installing.";
            if (!string.IsNullOrEmpty(help))
            {
                var tip = new ToolTip { AutoPopDelay = 30000 };
                tip.SetToolTip(input, help);
                tip.SetToolTip(lbl, help);
            }

            paramBoxes[p.Key] = box;
            PNL_Params.Controls.Add(lbl);
            PNL_Params.Controls.Add(input);
            if (!ReferenceEquals(input, box)) PNL_Params.Controls.Add(box);
        }
        PNL_Params.ResumeLayout();

        UpdatePreview();
    }

    /// <summary>Offers the timings the ROM says are plausible, selecting one when it is unambiguous.</summary>
    private void FillTimingBox(TimingSuggestion s)
    {
        CB_Timing.BeginUpdate();
        CB_Timing.Items.Clear();
        foreach (byte b in s.Options) CB_Timing.Items.Add($"0x{b:X2}");

        if (s.Certain) CB_Timing.Text = $"0x{s.Timing.Value:X2}";
        else if (s.Options.Count > 0) CB_Timing.Text = $"0x{s.Options[0]:X2}";
        else CB_Timing.Text = "0x00";

        CB_Timing.EndUpdate();
    }

    /// <summary>Reads the timing box, accepting `0x47` or a bare `47`, both hexadecimal.</summary>
    private bool TryReadTiming(out byte timing)
    {
        timing = 0;
        string text = (CB_Timing.Text ?? "").Trim();
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) text = text[2..];
        return byte.TryParse(text, System.Globalization.NumberStyles.HexNumber, null, out timing);
    }

    /// <summary>
    /// Renders the routine the current settings would produce, with addresses filled in when a ROM
    /// is loaded.
    /// </summary>
    private void UpdatePreview()
    {
        if (LB_Templates.SelectedItem is not FunctionTemplate t) { TB_Preview.Clear(); return; }

        List<string> body;
        try { body = t.Build(TB_Name.Text.Trim(), CurrentValues()).Assembly ?? []; }
        catch (Exception ex) { TB_Preview.Text = "Could not build with these values:\r\n" + ex.Message; return; }

        string text = string.Join(Environment.NewLine, body);

        // With a ROM loaded the tokens become real addresses, which is what will actually be
        // assembled. Without one they stay as names - still readable, and still complete.
        int unresolved = 0;
        if (symbols != null)
        {
            var sub = SymbolSubstitution.Apply(text, symbols);
            if (sub.Success) text = sub.Text;
            else unresolved = sub.Errors.Count;
        }

        var header = new List<string>
        {
            symbols == null
                ? "@ Preview. Routine names resolve to addresses when you install."
                : unresolved > 0
                    ? $"@ Preview. {unresolved} name(s) did not resolve against the loaded ROM."
                    : "@ Preview, resolved against the loaded ROM. This is what gets assembled.",
            "",
        };

        TB_Preview.Text = string.Join(Environment.NewLine, header) + text;
        TB_Preview.SelectionStart = 0;
        TB_Preview.ScrollToCaret();
    }
}

internal static class TemplateDisplayExtensions
{
    /// <summary>The template's mechanic kind, for display.</summary>
    public static string Mechanic(this FunctionTemplate t) => t.Kind.ToString();
}
